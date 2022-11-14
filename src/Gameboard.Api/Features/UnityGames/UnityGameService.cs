using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Gameboard.Api.Data;
using Gameboard.Api.Data.Abstractions;
using Gameboard.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TopoMojo.Api.Client;

namespace Gameboard.Api.Features.UnityGames;

internal class UnityGameService : _Service, IUnityGameService
{
    private readonly IChallengeStore _challengeStore;
    IUnityStore Store { get; }
    ITopoMojoApiClient Mojo { get; }

    private readonly ConsoleActorMap _actorMap;

    public UnityGameService(
            ILogger<UnityGameService> logger,
            IMapper mapper,
            CoreOptions options,
            IChallengeStore challengeStore,
            IUnityStore store,
            ITopoMojoApiClient mojo,
            ConsoleActorMap actorMap
        ) : base(logger, mapper, options)
    {
        Store = store;
        Mojo = mojo;
        _actorMap = actorMap;
        _challengeStore = challengeStore;
    }

    public async Task<Data.Challenge> AddChallenge(NewUnityChallenge newChallenge, User actor)
    {
        // each _team_ should only get one copy of the challenge. If anyone else tries, send them on their way
        // each player should only create their challenge data once, so if they call again, just return what they've already
        // got
        var existingChallenge = await Store.DbContext
            .Challenges
            .AsNoTracking()
            .Include(c => c.Events)
            .Where(c => c.GameId == newChallenge.GameId && c.TeamId == newChallenge.TeamId)
            .FirstOrDefaultAsync();

        if (existingChallenge != null)
        {
            return existingChallenge;
        }

        // otherwise, let's make some challenges
        // find the team's players
        var teamPlayers = await Store.DbContext
            .Players
            .Where(p => p.TeamId == newChallenge.TeamId)
            .ToListAsync();

        var teamCaptain = ResolveTeamCaptain(teamPlayers, newChallenge);
        var challengeName = $"{teamCaptain.ApprovedName} vs. Cubespace";

        // load the spec associated with the game
        var challengeSpec = await Store.DbContext.ChallengeSpecs.FirstOrDefaultAsync(c => c.GameId == newChallenge.GameId);
        if (challengeSpec == null)
        {
            throw new SpecNotFound(newChallenge.GameId);
        }

        // we have to spoof topomojo data here. load the game and related data.
        var game = await this.Store
            .DbContext
            .Games
            .FirstOrDefaultAsync(g => g.Id == newChallenge.GameId);

        if (game == null)
        {
            throw new ResourceNotFound<Game>(newChallenge.GameId);
        }

        // this is some guesswork on my part and omits some fields.
        // we'll see how it goes - BS
        var state = new TopoMojo.Api.Client.GameState
        {
            Id = newChallenge.GameId,
            Name = game.Id,
            ManagerId = teamCaptain.Id,
            ManagerName = teamCaptain.ApprovedName,
            Markdown = game.GameMarkdown,
            Players = teamPlayers.Select(p => new TopoMojo.Api.Client.Player
            {
                GamespaceId = newChallenge.GamespaceId,
                SubjectId = p.Id,
                SubjectName = p.ApprovedName,
                Permission = p.IsManager ? TopoMojo.Api.Client.Permission.Manager : TopoMojo.Api.Client.Permission.None,
                IsManager = p.IsManager
            }).ToArray(),
            StartTime = game.GameStart,
            EndTime = game.GameEnd,
            ExpirationTime = game.GameStart.AddMinutes(game.SessionMinutes),
            WhenCreated = DateTimeOffset.UtcNow,
            Challenge = new ChallengeView()
            {
                Attempts = 1,
                LastScoreTime = DateTimeOffset.MinValue,
                MaxPoints = newChallenge.MaxPoints,
                Text = challengeName
            },
            IsActive = game.IsLive,
            Vms = newChallenge.Vms.Select(vm => new VmState
            {
                Id = vm.Id,
                Name = vm.Name,
                IsolationId = newChallenge.GamespaceId,
                IsVisible = true
            }).ToList()
        };

        var newChallengeEntity = new Data.Challenge
        {
            Id = newChallenge.GamespaceId,
            Name = $"{teamCaptain.ApprovedName} vs. Cubespace",
            GameId = challengeSpec.GameId,
            TeamId = newChallenge.TeamId,
            PlayerId = newChallenge.PlayerId,
            HasDeployedGamespace = true,
            SpecId = challengeSpec.Id,
            StartTime = DateTimeOffset.UtcNow,
            LastSyncTime = DateTimeOffset.UtcNow,
            State = JsonSerializer.Serialize(state),
            GraderKey = Guid.NewGuid().ToString("n").ToSha256(),
            Points = newChallenge.MaxPoints,
            Score = 0,
            Events = new List<Data.ChallengeEvent>
            {  
                // an initial event to start the party
                new ChallengeEvent
                {
                    Id = Guid.NewGuid().ToString("n"),
                    UserId = actor.Id,
                    TeamId = newChallenge.TeamId,
                    Timestamp = DateTimeOffset.UtcNow,
                    Text = $"{teamCaptain.ApprovedName}'s journey into CubeSpace has begun...",
                    Type = ChallengeEventType.Started
                }
            },
            WhenCreated = DateTimeOffset.UtcNow,
        };

        await Store.DbContext.Challenges.AddAsync(newChallengeEntity);
        await Store.DbContext.SaveChangesAsync();

        return newChallengeEntity;
    }

    public async Task<ChallengeEvent> AddChallengeEvent(NewUnityChallengeEvent model, string userId)
    {
        var challengeEvent = new Data.ChallengeEvent
        {
            ChallengeId = model.ChallengeId,
            UserId = userId,
            TeamId = model.TeamId,
            Text = model.Text,
            Type = model.Type,
            Timestamp = model.Timestamp
        };

        return await Store.AddUnityChallengeEvent(challengeEvent);
    }

    public async Task DeleteChallengeData(string gameId)
    {
        var challenges = await Store
            .DbContext
            .Challenges
            .Include(c => c.Events)
            .Where(c => c.GameId == gameId)
            .ToListAsync();

        if (challenges.Count == 0)
        {
            return;
        }

        Store.DbContext.Challenges.RemoveRange(challenges);
        Store.DbContext.ChallengeEvents.RemoveRange(challenges.SelectMany(c => c.Events));

        await Store.DbContext.SaveChangesAsync();
    }

    public async Task CreateMissionEvent(UnityMissionUpdate model, Api.User actor)
    {
        var challenge = await Store.DbContext
            .Challenges
            .Include(c => c.Game)
            .Include(c => c.Events)
            .Include(c => c.Player)
            .Where(c => c.TeamId == model.TeamId && c.Game.Mode.ToLower() == "unity")
            .FirstOrDefaultAsync();

        if (challenge == null)
        {
            throw new ChallengeResolutionFailure(model.TeamId);
        }

        // record an event for this challenge
        challenge.Events.Add(new Data.ChallengeEvent
        {
            Id = Guid.NewGuid().ToString("n"),
            ChallengeId = challenge.Id,
            UserId = actor.Id,
            TeamId = model.TeamId,
            Text = $"{challenge.Player} has found the codex for {model.MissionName}!",
            Type = ChallengeEventType.Submission,
            Timestamp = DateTimeOffset.UtcNow
        });

        // also update the score of the challenge
        challenge.Score += model.PointsScored;

        // save it up
        await Store.DbContext.SaveChangesAsync();
    }

    private Data.Player ResolveTeamCaptain(IEnumerable<Data.Player> players, NewUnityChallenge newChallenge)
    {
        if (players.Count() == 0)
        {
            throw new CaptainResolutionFailure(newChallenge.TeamId);
        }

        // if the team has a captain (manager, yay)
        // if they have too many, boo (pick one by name which is stupid but stupid things happen sometimes)
        // if they have none, congratulations to the player who called the API!
        var sortedPlayers = players.OrderBy(p => p.ApprovedName);
        var actingPlayer = players.First(p => p.Id == newChallenge.PlayerId);
        var captains = players.Where(p => p.IsManager);

        if (captains.Count() == 1)
        {
            return captains.First();
        }
        else if (captains.Count() > 1)
        {
            return captains.OrderBy(c => c.ApprovedName).First();
        }

        return actingPlayer;
    }
}