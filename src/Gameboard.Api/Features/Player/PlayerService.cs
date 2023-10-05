// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Gameboard.Api.Common.Services;
using Gameboard.Api.Data;
using Gameboard.Api.Data.Abstractions;
using Gameboard.Api.Features.GameEngine;
using Gameboard.Api.Features.Games;
using Gameboard.Api.Features.Games.Start;
using Gameboard.Api.Features.Player;
using Gameboard.Api.Features.Practice;
using Gameboard.Api.Features.Teams;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Gameboard.Api.Services;

public class PlayerService
{
    private readonly IGameEngineService _gameEngine;
    private readonly IMediator _mediator;
    private readonly IPracticeChallengeScoringListener _practiceChallengeScoringListener;
    private readonly TimeSpan _idmapExpiration = new(0, 30, 0);
    private readonly INowService _now;
    private readonly IPracticeService _practiceService;
    private readonly IStore _store;
    private readonly ITeamService _teamService;

    CoreOptions CoreOptions { get; }
    ChallengeService ChallengeService { get; set; }
    IPlayerStore PlayerStore { get; }
    IGameService GameService { get; }
    IGameStartService GameStartService { get; }
    IGameStore GameStore { get; }
    IGuidService GuidService { get; }
    IInternalHubBus HubBus { get; }
    IMapper Mapper { get; }
    IMemoryCache LocalCache { get; }

    public PlayerService(
        ChallengeService challengeService,
        CoreOptions coreOptions,
        IGameEngineService gameEngine,
        IGameService gameService,
        IGameStartService gameStartService,
        IGameStore gameStore,
        IGuidService guidService,
        IInternalHubBus hubBus,
        IMapper mapper,
        IMediator mediator,
        IMemoryCache memCache,
        INowService now,
        IPlayerStore playerStore,
        IPracticeChallengeScoringListener practiceChallengeScoringListener,
        IPracticeService practiceService,
        IStore store,
        ITeamService teamService
    )
    {
        ChallengeService = challengeService;
        CoreOptions = coreOptions;
        _gameEngine = gameEngine;
        CoreOptions = coreOptions;
        GameService = gameService;
        GuidService = guidService;
        _mediator = mediator;
        _practiceChallengeScoringListener = practiceChallengeScoringListener;
        _practiceService = practiceService;
        _now = now;
        GameStartService = gameStartService;
        GameStore = gameStore;
        HubBus = hubBus;
        LocalCache = memCache;
        Mapper = mapper;
        PlayerStore = playerStore;
        _store = store;
        _teamService = teamService;
    }

    public async Task<Player> Enroll(NewPlayer model, User actor, CancellationToken cancellationToken)
    {
        var game = await GameStore.Retrieve(model.GameId);
        var user = await _store
            .WithNoTracking<Data.User>()
            .Include(u => u.Sponsor)
            // include registrations for this game and type (because we validate whether they have active registrations later)
            .Include(u => u.Enrollments.Where(p => p.GameId == model.GameId && p.Mode == game.PlayerMode))
            .SingleAsync(u => u.Id == model.UserId, cancellationToken);

        if (user.HasDefaultSponsor)
            throw new CantEnrollWithDefaultSponsor(model.UserId, model.GameId);

        if (user.SponsorId.IsEmpty())
            throw new NoPlayerSponsorForGame(model.UserId, model.GameId);

        if (game.IsPracticeMode)
            return await RegisterPracticeSession(model, user, cancellationToken);

        if (!game.RegistrationActive && !(actor.IsRegistrar || actor.IsTester || actor.IsAdmin))
            throw new RegistrationIsClosed(model.GameId);

        // while this collection will always only contain the correct player records (because of the filtered include above),
        // we have to specify our criteria again here because mock providers for unit tests seem to ignore filtered includes
        if (user.Enrollments.Any(p => p.GameId == game.Id && p.Mode == game.PlayerMode))
            throw new AlreadyRegistered(model.UserId, model.GameId);

        var entity = InitializePlayer(model, user, game.SessionMinutes);

        await PlayerStore.Create(entity);
        await HubBus.SendPlayerEnrolled(Mapper.Map<Player>(entity), actor);

        if (game.RequireSynchronizedStart)
            await GameStartService.HandleSyncStartStateChanged(entity.GameId, cancellationToken);

        // the initialized Data.Player only has the SponsorId, and we want to send down the complete
        // sponsor object. We could just manually attach it, but for now we're just going to reload
        // the entity from the DB to handle future property wireups
        return Mapper.Map<Player>
        (
            await _store
                .WithNoTracking<Data.Player>()
                .Include(p => p.Sponsor)
                .SingleAsync(p => p.Id == entity.Id)
        );
    }

    /// <summary>
    /// Maps a PlayerId to its UserId
    /// </summary>
    /// <remarks>This happens frequently for authorization, so cache the mapping.</remarks>
    /// <param name="playerId"></param>
    /// <returns></returns>
    public async Task<string> MapId(string playerId)
    {
        if (LocalCache.TryGetValue(playerId, out string userId))
            return userId;

        userId = (await PlayerStore.Retrieve(playerId))?.UserId;
        LocalCache.Set(playerId, userId, _idmapExpiration);

        return userId;
    }

    public async Task<Data.Player> RetrieveByUserId(string userId)
    // TODO: possibly cache the opposite direction too
        => await _store
            .WithNoTracking<Data.Player>()
            .Where(p => p.UserId == userId)
            .FirstOrDefaultAsync();

    public async Task<Player> Retrieve(string id)
    {
        return Mapper.Map<Player>(await PlayerStore.Retrieve(id));
    }

    public async Task<Player> Update(ChangedPlayer model, User actor, bool sudo = false)
    {
        var entity = await PlayerStore.Retrieve(model.Id);
        var prev = Mapper.Map<Player>(entity);

        if (!sudo)
        {
            Mapper.Map(
                Mapper.Map<SelfChangedPlayer>(model),
                entity
            );

            entity.NameStatus = entity.Name != entity.ApprovedName ? "pending" : string.Empty;
        }
        else
        {
            Mapper.Map(model, entity);
        }

        if (prev.Name != entity.Name)
        {
            // check uniqueness
            bool found = await PlayerStore.DbSet.AnyAsync(p =>
                p.GameId == entity.GameId &&
                p.TeamId != entity.TeamId &&
                p.Name == entity.Name
            );

            if (found)
                entity.NameStatus = AppConstants.NameStatusNotUnique;
        }

        await PlayerStore.Update(entity);
        await HubBus.SendTeamUpdated(Mapper.Map<Player>(entity), actor);
        return Mapper.Map<Player>(entity);
    }

    public async Task<Player> StartSession(SessionStartRequest model, User actor, bool sudo)
    {
        var team = await PlayerStore.ListTeamByPlayer(model.PlayerId);

        var player = team.First();
        var game = await PlayerStore.DbContext.Games.SingleOrDefaultAsync(g => g.Id == player.GameId);

        // rule: game's execution period has to be open
        if (!sudo && game.IsLive.Equals(false))
            throw new GameNotActive();

        // rule: players per team has to be within the game's constraint
        if (
            !sudo &&
            game.RequireTeam &&
            team.Length < game.MinTeamSize
        )
            throw new InvalidTeamSize();

        // rule: for now, can't start a player's session in this code path.
        // TODO: refactor for SOLIDness
        if (!sudo && game.RequireSynchronizedStart)
        {
            throw new InvalidOperationException("Can't start a player's session for a sync start game with PlayerService.StartSession (use GameService.StartSynchronizedSession).");
        }

        // rule: teams can't have a session limit exceeding the game's settings
        if (!sudo && game.SessionLimit > 0)
        {
            var ts = DateTimeOffset.UtcNow;

            int sessionCount = await PlayerStore.DbSet
                .CountAsync(p =>
                    p.GameId == game.Id &&
                    p.Role == PlayerRole.Manager &&
                    ts < p.SessionEnd
                );

            if (sessionCount >= game.SessionLimit)
                throw new SessionLimitReached(player.TeamId, game.Id, sessionCount, game.SessionLimit);
        }

        var sessionWindow = CalculateSessionWindow(game, _now.Get());

        foreach (var p in team)
        {
            p.SessionMinutes = game.SessionMinutes;
            p.SessionBegin = sessionWindow.Start;
            p.SessionEnd = sessionWindow.End;
        }

        await PlayerStore.Update(team);

        if (player.Score > 0)
        {
            var challenge = new Data.Challenge
            {
                Id = Guid.NewGuid().ToString("n"),
                PlayerId = player.Id,
                TeamId = player.TeamId,
                GameId = player.GameId,
                SpecId = "_initialscore_",
                Name = "_initialscore_",
                Points = player.Score,
                Score = player.Score,
            };

            PlayerStore.DbContext.Add(challenge);
            await PlayerStore.DbContext.SaveChangesAsync();
        }

        var asViewModel = Mapper.Map<Player>(player);
        await HubBus.SendTeamSessionStarted(asViewModel, actor);

        return asViewModel;
    }

    public DateRange CalculateSessionWindow(Data.Game game, DateTimeOffset sessionStart)
        => new()
        {
            Start = sessionStart,
            End = sessionStart.AddMinutes(game.SessionMinutes)
        };

    public async Task<Player[]> List(PlayerDataFilter model, bool sudo = false)
    {
        if (!sudo && !model.WantsGame && !model.WantsTeam)
            return Array.Empty<Player>();

        var q = BuildListQuery(model);
        var players = await Mapper.ProjectTo<Player>(q).ToArrayAsync();
        var queriedPlayerIds = players.Select(p => p.Id).ToArray();

        // We used to store the team's sponsors (technically, the logo files of their sponsors)
        // in the players table as a delimited string column. This had the advantage of making it easy to pull back
        // a team's logos alongside a player record (useful in some views), but resulted in the
        // need to manually maintain the column and got complicated if the logo file changed or something.
        // As part of our change to the Sponsor schema, we now just query player sponsors by team Id and 
        // append logos here.
        var teamSponsors = await _store
            .WithNoTracking<Data.Player>()
                .Include(p => p.Sponsor)
            .Where(p => queriedPlayerIds.Contains(p.Id))
            .Select(p => new
            {
                p.TeamId,
                SponsorLogoFileNames = p.Sponsor.Logo
            })
            .GroupBy(g => g.TeamId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(thing => thing.SponsorLogoFileNames).ToArray());

        foreach (var player in players)
            if (teamSponsors.ContainsKey(player.TeamId))
                player.TeamSponsorLogos = teamSponsors[player.TeamId];

        return players;
    }

    public async Task<Standing[]> Standings(PlayerDataFilter model)
    {
        if (model.gid.IsEmpty())
            return Array.Empty<Standing>();

        model.Filter = model.Filter
            .Append(PlayerDataFilter.FilterScoredOnly)
            .ToArray();

        model.mode = PlayerMode.Competition.ToString();

        var q = BuildListQuery(model);

        return await Mapper.ProjectTo<Standing>(q).ToArrayAsync();
    }

    private IQueryable<Data.Player> BuildListQuery(PlayerDataFilter model)
    {
        var ts = _now.Get();

        var q = PlayerStore.List()
            .Include(p => p.User)
            .Include(p => p.Sponsor)
            .AsNoTracking();

        if (model.WantsMode)
            q = q.Where(p => p.Mode == Enum.Parse<PlayerMode>(model.mode, true));

        if (model.WantsGame)
        {
            q = q.Where(p => p.GameId == model.gid);

            if (model.WantsOrg)
                q = q.Where(p => p.SponsorId == model.org);
        }

        if (model.WantsUser)
            q = q.Where(p => p.UserId == model.uid);

        if (model.WantsTeam)
            q = q.Where(p => p.TeamId == model.tid);

        if (model.WantsCollapsed || model.WantsActive || model.WantsScored)
            q = q.Where(p => p.Role == PlayerRole.Manager);

        if (model.WantsActive)
            q = q.Where(p => p.SessionBegin < ts && p.SessionEnd > ts);

        if (model.WantsComplete)
            q = q.Where(p => p.SessionEnd > DateTimeOffset.MinValue);

        if (model.WantsAdvanced)
            q = q.Where(p => p.Advanced);

        if (model.WantsDismissed)
            q = q.Where(p => !p.Advanced);

        if (model.WantsPending)
            q = q.Where(u => u.NameStatus.Equals(AppConstants.NameStatusPending));

        if (model.WantsDisallowed)
            q = q.Where(u => !string.IsNullOrEmpty(u.NameStatus) && !u.NameStatus.Equals(AppConstants.NameStatusPending));

        if (model.WantsScored)
            q = q.WhereIsScoringPlayer();

        if (model.Term.NotEmpty())
        {
            var term = model.Term.ToLower();

            q = q.Where(p =>
                p.ApprovedName.ToLower().Contains(term) ||
                p.Name.ToLower().Contains(term) ||
                p.Id.StartsWith(term) ||
                p.TeamId.StartsWith(term) ||
                p.UserId.StartsWith(term) ||
                p.Sponsor.Name.StartsWith(term) ||
                p.User.Name.ToLower().Contains(term) ||
                p.User.ApprovedName.ToLower().Contains(term) ||
                PlayerStore.DbSet.Where(p2 => p2.TeamId == p.TeamId && (p2.UserId.StartsWith(term) || p2.User.ApprovedName.ToLower().Contains(term))).Any()
            );
        }

        // TODO: maybe just sort on rank here
        if (model.WantsSortByRank)
            q = q.OrderByDescending(p => p.Score)
                .ThenBy(p => p.Time)
                .ThenByDescending(p => p.CorrectCount)
                .ThenByDescending(p => p.PartialCount)
                .ThenBy(p => p.Rank)
                .ThenBy(p => p.ApprovedName);

        if (model.WantsSortByTime)
            q = q.OrderByDescending(p => p.SessionBegin);

        if (model.WantsSortByName)
            q = q.OrderBy(p => p.ApprovedName)
                .ThenBy(p => p.User.ApprovedName);

        q = q.Skip(model.Skip);

        if (model.Take > 0)
            q = q.Take(model.Take);

        return q;
    }

    public async Task<BoardPlayer> LoadBoard(string id)
    {
        var mapped = Mapper.Map<BoardPlayer>(
            await PlayerStore.LoadBoard(id)
        );

        mapped.ChallengeDocUrl = CoreOptions.ChallengeDocUrl;
        return mapped;
    }

    public async Task<TeamInvitation> GenerateInvitation(string id)
    {
        var player = await PlayerStore.Retrieve(id);

        if (player.Role != PlayerRole.Manager)
            throw new ActionForbidden();

        byte[] buffer = new byte[16];

        new Random().NextBytes(buffer);

        player.InviteCode = Convert.ToBase64String(buffer)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
        ;

        await PlayerStore.Update(player);

        return new TeamInvitation
        {
            Code = player.InviteCode
        };
    }

    public async Task<Player> Enlist(PlayerEnlistment model, User actor, CancellationToken cancellationToken)
    {
        var sudo = actor.IsRegistrar;

        var player = await PlayerStore.DbSet.FirstOrDefaultAsync(p => p.Id == model.PlayerId) ?? throw new ResourceNotFound<Player>(model.PlayerId);
        var playersWithThisCode = await _store
            .WithNoTracking<Data.Player>()
            .Where(p => p.InviteCode == model.Code)
            .ToArrayAsync();

        var teamIds = playersWithThisCode.Select(p => p.TeamId).Distinct().ToArray();
        if (teamIds.Length != 1)
            throw new CantResolveTeamFromCode(model.Code, teamIds);

        var manager = _teamService.ResolveCaptain(playersWithThisCode);

        if (player.GameId != manager.GameId)
            throw new NotYetRegistered(player.Id, manager.GameId);

        if (player.Id == manager.Id)
            return Mapper.Map<Player>(player);

        var game = await _store.SingleAsync<Data.Game>(manager.GameId, cancellationToken);

        if (!sudo && !game.RegistrationActive)
            throw new RegistrationIsClosed(manager.GameId);

        if (!sudo && manager.SessionBegin.Year > 1)
            throw new RegistrationIsClosed(manager.GameId, "Registration begins in more than a year.");

        if (!sudo && manager.Game.RequireSponsoredTeam && manager.SponsorId != player.SponsorId)
            throw new RequiresSameSponsor(manager.GameId, manager.Id, manager.Sponsor.Name, player.Id, player.Sponsor.Name);

        int count = await PlayerStore.List().CountAsync(p => p.TeamId == manager.TeamId);

        if (!sudo && manager.Game.AllowTeam && count >= manager.Game.MaxTeamSize)
            throw new TeamIsFull(manager.Id, count, manager.Game.MaxTeamSize);

        player.TeamId = manager.TeamId;
        player.Role = PlayerRole.Member;
        player.InviteCode = model.Code;

        await PlayerStore.Update(player);

        var mappedPlayer = Mapper.Map<Player>(player);
        await HubBus.SendPlayerEnrolled(mappedPlayer, actor);
        return mappedPlayer;
    }

    public async Task Unenroll(PlayerUnenrollRequest request, CancellationToken cancellationToken)
    {
        // they probably don't have challenge data on an unenroll, but in case an admin does this
        // or something, we'll clean up their challenges
        var player = await PlayerStore.Retrieve(request.PlayerId, players => players.Include(p => p.Game));
        await ChallengeService.ArchivePlayerChallenges(player);

        // delete the player record
        await PlayerStore.Delete(request.PlayerId);

        // notify listeners on SignalR (like the team)
        var playerModel = Mapper.Map<Player>(player);
        await HubBus.SendPlayerLeft(playerModel, request.Actor);

        // update sync start if needed
        if (player.Game.RequireSynchronizedStart)
            await GameStartService.HandleSyncStartStateChanged(playerModel.GameId, cancellationToken);
    }

    public async Task<TeamChallenge[]> LoadChallengesForTeam(string teamId)
    {
        return Mapper.Map<TeamChallenge[]>(await PlayerStore.ListTeamChallenges(teamId));
    }

    public async Task<TeamSummary[]> LoadTeams(string id, bool sudo)
    {
        var players = await PlayerStore.List()
            .Where(p => p.GameId == id)
            .ToArrayAsync();

        var teams = players
            .GroupBy(p => p.TeamId)
            .Select(g => new TeamSummary
            {
                Id = g.Key,
                Name = g.First().ApprovedName,
                Sponsor = g.First().Sponsor.Logo,
                Members = g.Select(i => i.UserId).ToArray()
            })
            .ToArray()
        ;

        return teams;
    }

    public async Task<IEnumerable<Team>> ObserveTeams(string id)
    {
        var players = await PlayerStore.List()
            .Where(p => p.GameId == id)
            .Include(p => p.User)
            .ToArrayAsync();

        var captains = players
            .Where(p => p.IsManager)
            .Where(p => p.IsLive)
            .GroupBy(p => p.TeamId)
            .ToDictionary(g => g.Key, g => g.First());

        var teams = captains
            .Values
            .Select(c => new Team
            {
                TeamId = c.TeamId,
                ApprovedName = c.ApprovedName,
                Sponsors = Mapper.Map<Sponsor[]>(players.Where(p => p.TeamId == c.TeamId).Select(p => p.Sponsor)),
                GameId = c.GameId,
                SessionBegin = c.SessionBegin,
                SessionEnd = c.SessionEnd,
                Rank = c.Rank,
                Score = c.Score,
                Time = c.Time,
                CorrectCount = c.CorrectCount,
                PartialCount = c.PartialCount,
                Advanced = c.Advanced,
                Members = players.Where(p => p.TeamId == c.TeamId).Select(i => new TeamMember
                {
                    Id = i.UserId,
                    ApprovedName = i.User.ApprovedName,
                    Role = i.Role,
                    UserId = i.UserId
                }).OrderBy(p => p.ApprovedName).ToArray()
            })
            .OrderBy(c => c.ApprovedName)
            .ToArray();

        return teams;
    }

    public async Task AdvanceTeams(TeamAdvancement model)
    {
        var game = await GameStore.Retrieve(model.NextGameId);

        var allteams = await PlayerStore.List()
            .Where(p => p.GameId == model.GameId)
            .ToArrayAsync();

        var teams = allteams.GroupBy(p => p.TeamId)
            .Where(g => model.TeamIds.Contains(g.Key))
            .ToArray();

        var enrollments = new List<Data.Player>();

        foreach (var team in teams)
        {
            string newId = Guid.NewGuid().ToString("n");

            foreach (var player in team)
            {
                player.Advanced = true;

                enrollments.Add(new Data.Player
                {
                    TeamId = newId,
                    UserId = player.UserId,
                    GameId = model.NextGameId,
                    ApprovedName = player.ApprovedName,
                    Name = player.Name,
                    SponsorId = player.SponsorId,
                    Role = player.Role,
                    Score = model.WithScores ? player.Score : 0
                });
            }
        }

        await PlayerStore.Create(enrollments);
        await PlayerStore.Update(allteams);
    }

    public Task<Player> AdjustSessionEnd(SessionChangeRequest model, User actor, CancellationToken cancellationToken)
        => _practiceChallengeScoringListener.AdjustSessionEnd(model, actor, cancellationToken);

    public async Task<PlayerCertificate> MakeCertificate(string id)
    {
        var player = await PlayerStore.List()
            .Include(p => p.Game)
            .Include(p => p.User)
                .ThenInclude(u => u.PublishedCompetitiveCertificates)
            .FirstOrDefaultAsync(p => p.Id == id);

        var playerCount = await PlayerStore.DbSet
            .Where(p => p.GameId == player.GameId &&
                p.SessionEnd > DateTimeOffset.MinValue)
            .CountAsync();

        var teamCount = await PlayerStore.DbSet
            .Where(p => p.GameId == player.GameId &&
                p.SessionEnd > DateTimeOffset.MinValue)
            .GroupBy(p => p.TeamId)
            .CountAsync();

        return CertificateFromTemplate(player, playerCount, teamCount);
    }

    public async Task<IEnumerable<PlayerCertificate>> MakeCertificates(string uid)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var completedSessions = await PlayerStore.List()
            .Include(p => p.Game)
            .Include(p => p.User)
                .ThenInclude(u => u.PublishedCompetitiveCertificates)
            .Where
            (
                p => p.UserId == uid &&
                p.SessionEnd > DateTimeOffset.MinValue &&
                p.Game.GameEnd < now &&
                p.Game.CertificateTemplate != null &&
                p.Game.CertificateTemplate.Length > 0
            )
            .WhereIsScoringPlayer()
            .OrderByDescending(p => p.Game.GameEnd)
            .ToArrayAsync();

return completedSessions.Select(c => CertificateFromTemplate(c,
            PlayerStore.DbSet
                .Where(p => p.Game == c.Game &&
                    p.SessionEnd > DateTimeOffset.MinValue)
                .WhereIsScoringPlayer()
                .Count(),
            PlayerStore.DbSet
                .Where(p => p.Game == c.Game &&
                    p.SessionEnd > DateTimeOffset.MinValue)
                .WhereIsScoringPlayer()
                .GroupBy(p => p.TeamId).Count()
        )).ToArray();
    }

    private PlayerCertificate CertificateFromTemplate(Data.Player player, int playerCount, int teamCount)
    {
        var certificateHTML = player.Game.CertificateTemplate;
        if (certificateHTML.IsEmpty())
            return null;

        certificateHTML = certificateHTML.Replace("{{leaderboard_name}}", player.ApprovedName);
        certificateHTML = certificateHTML.Replace("{{user_name}}", player.User.ApprovedName);
        certificateHTML = certificateHTML.Replace("{{score}}", player.Score.ToString());
        certificateHTML = certificateHTML.Replace("{{rank}}", player.Rank.ToString());
        certificateHTML = certificateHTML.Replace("{{game_name}}", player.Game.Name);
        certificateHTML = certificateHTML.Replace("{{competition}}", player.Game.Competition);
        certificateHTML = certificateHTML.Replace("{{season}}", player.Game.Season);
        certificateHTML = certificateHTML.Replace("{{track}}", player.Game.Track);
        certificateHTML = certificateHTML.Replace("{{date}}", player.SessionEnd.ToString("MMMM dd, yyyy"));
        certificateHTML = certificateHTML.Replace("{{player_count}}", playerCount.ToString());
        certificateHTML = certificateHTML.Replace("{{team_count}}", teamCount.ToString());

        return new Api.PlayerCertificate
        {
            Game = Mapper.Map<Game>(player.Game),
            PublishedOn = player.User.PublishedCompetitiveCertificates.FirstOrDefault(c => c.GameId == player.Game.Id)?.PublishedOn,
            Player = Mapper.Map<Player>(player),
            Html = certificateHTML
        };
    }

    private async Task<Player> RegisterPracticeSession(NewPlayer model, Data.User user, CancellationToken cancellationToken)
    {
        // load practice settings
        var settings = await _practiceService.GetSettings(cancellationToken);

        // check for existing sessions
        var nowStamp = _now.Get();

        var players = await PlayerStore.ListWithNoTracking().Where
        (
            p =>
                p.UserId == model.UserId &&
                p.Mode == PlayerMode.Practice &&
                p.SessionEnd > nowStamp
        ).ToArrayAsync(cancellationToken);

        if (players.Any(p => p.GameId == model.GameId))
            return Mapper.Map<Player>(players.First(p => p.GameId == model.GameId));

        // find gamespaces across all practice sessions
        var teamIds = players.Select(p => p.TeamId).ToArray();
        var game = await GameStore.Retrieve(model.GameId);
        foreach (var teamId in teamIds)
        {
            // practice mode only allows a single gamespace
            if (await _teamService.IsAtGamespaceLimit(teamId, game, cancellationToken))
                throw new UserLevelPracticeGamespaceLimitReached(model.UserId, model.GameId, teamIds);
        }

        // don't exceed global configured limit
        if (settings.MaxConcurrentPracticeSessions.HasValue)
        {
            int count = await PlayerStore.DbSet.CountAsync(p =>
                p.Mode == PlayerMode.Practice &&
                p.SessionEnd > nowStamp, cancellationToken);

            if (count >= settings.MaxConcurrentPracticeSessions.Value)
                throw new PracticeSessionLimitReached(model.UserId, count, settings.MaxConcurrentPracticeSessions.Value);
        }

        var entity = InitializePlayer(model, user, settings.DefaultPracticeSessionLengthMinutes);

        // start session
        entity.SessionBegin = nowStamp;
        entity.SessionEnd = entity.SessionBegin.AddMinutes(entity.SessionMinutes);
        entity.Mode = PlayerMode.Practice;

        await PlayerStore.Create(entity);
        return Mapper.Map<Player>(entity);
    }

    private Data.Player InitializePlayer(NewPlayer model, Data.User user, int duration)
        => new()
        {
            ApprovedName = user.ApprovedName,
            GameId = model.GameId,
            Name = user.ApprovedName,
            Role = PlayerRole.Manager,
            SessionMinutes = duration,
            SponsorId = user.SponsorId,
            TeamId = GuidService.GetGuid(),
            UserId = model.UserId,
            WhenCreated = _now.Get()
        };
}
