using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Gameboard.Api.Common;
using Gameboard.Api.Data;
using Gameboard.Api.Features.Games;
using Gameboard.Api.Features.Player;
using Gameboard.Api.Hubs;
using Gameboard.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Gameboard.Api.Features.Teams;

public interface ITeamService
{
    Task<IEnumerable<SimpleEntity>> GetChallengesWithActiveGamespace(string teamId, string gameId, CancellationToken cancellationToken);
    Task<bool> GetExists(string teamId);
    Task<int> GetSessionCount(string teamId, string gameId);
    Task<Team> GetTeam(string id);
    Task<bool> IsAtGamespaceLimit(string teamId, Data.Game game, CancellationToken cancellationToken);
    Task<bool> IsOnTeam(string teamId, string userId);
    Task<Data.Player> ResolveCaptain(string teamId);
    Task<Data.Player> ResolveCaptain(IEnumerable<Data.Player> players);
    Task PromoteCaptain(string teamId, string newCaptainPlayerId, User actingUser);
}

internal class TeamService : ITeamService
{
    private readonly IMapper _mapper;
    private readonly IMemoryCache _memCache;
    private readonly INowService _now;
    private readonly IInternalHubBus _teamHubService;
    private readonly IPlayerStore _playerStore;
    private readonly IStore _store;

    public TeamService
    (
        IMapper mapper,
        IMemoryCache memCache,
        INowService now,
        IInternalHubBus teamHubService,
        IPlayerStore playerStore,
        IStore store
    )
    {
        _mapper = mapper;
        _memCache = memCache;
        _now = now;
        _playerStore = playerStore;
        _store = store;
        _teamHubService = teamHubService;
    }

    public async Task<IEnumerable<SimpleEntity>> GetChallengesWithActiveGamespace(string teamId, string gameId, CancellationToken cancellationToken)
        => await _store
            .List<Data.Challenge>()
            .Where(c => c.TeamId == teamId)
            .Where(c => c.GameId == gameId)
            .Where(c => c.HasDeployedGamespace == true)
            .Select(c => new SimpleEntity { Id = c.Id, Name = c.Name })
            .ToArrayAsync(cancellationToken);

    public async Task<bool> GetExists(string teamId)
        => await _playerStore.ListTeam(teamId).AnyAsync();

    public async Task<int> GetSessionCount(string teamId, string gameId)
    {
        var now = _now.Get();

        return await _playerStore
            .List()
            .CountAsync
            (
                p =>
                    p.GameId == gameId &&
                    p.Role == PlayerRole.Manager &&
                    now < p.SessionEnd
            );
    }

    public async Task<Team> GetTeam(string id)
    {
        var players = await _playerStore.ListTeam(id).ToArrayAsync();
        if (players.Length == 0)
            return null;

        var team = _mapper.Map<Team>(players.First(p => p.IsManager));

        team.Members = _mapper.Map<TeamMember[]>(players.Select(p => p.User));
        team.Sponsors = _mapper.Map<Sponsor[]>(players.Select(p => p.Sponsor));

        return team;
    }

    public async Task<bool> IsAtGamespaceLimit(string teamId, Data.Game game, CancellationToken cancellationToken)
    {
        var activeGameChallenges = await GetChallengesWithActiveGamespace(teamId, game.Id, cancellationToken);
        return activeGameChallenges.Count() >= game.GetGamespaceLimit();
    }

    public async Task<bool> IsOnTeam(string teamId, string userId)
    {
        // simple serialize to indicate whether this user and team are a match
        var cacheKey = $"{teamId}|{userId}";

        if (_memCache.TryGetValue(cacheKey, out bool cachedIsOnTeam))
            return cachedIsOnTeam;

        var teamUserIds = await _playerStore
            .ListTeam(teamId)
            .Select(p => p.UserId).ToArrayAsync();

        var isOnTeam = teamUserIds.Contains(userId);
        _memCache.Set(cacheKey, isOnTeam, TimeSpan.FromMinutes(30));

        return isOnTeam;
    }

    public async Task PromoteCaptain(string teamId, string newCaptainPlayerId, User actingUser)
    {
        var teamPlayers = await _playerStore
            .List()
            .AsNoTracking()
            .Where(p => p.TeamId == teamId)
            .ToListAsync();

        var oldCaptain = teamPlayers.SingleOrDefault(p => p.Role == PlayerRole.Manager);
        var newCaptain = teamPlayers.Single(p => p.Id == newCaptainPlayerId);

        using (var transaction = await _playerStore.DbContext.Database.BeginTransactionAsync())
        {
            await _playerStore
                .List()
                .Where(p => p.TeamId == teamId)
                .ExecuteUpdateAsync(p => p.SetProperty(p => p.Role, p => PlayerRole.Member));

            var affectedPlayers = await _playerStore
                .List()
                .Where(p => p.Id == newCaptainPlayerId)
                .ExecuteUpdateAsync
                (
                    p => p.SetProperty(p => p.Role, p => PlayerRole.Manager)
                );

            // this automatically rolls back the transaction
            if (affectedPlayers != 1)
                throw new PromotionFailed(teamId, newCaptainPlayerId, affectedPlayers);

            await transaction.CommitAsync();
        }

        await _teamHubService.SendPlayerRoleChanged(_mapper.Map<Api.Player>(newCaptain), actingUser);
    }

    public Task<Data.Player> ResolveCaptain(string teamId)
    {
        return ResolveCaptain(teamId, null);
    }

    public Task<Data.Player> ResolveCaptain(IEnumerable<Data.Player> players)
    {
        return ResolveCaptain(null, players);
    }

    private async Task<Data.Player> ResolveCaptain(string teamId, IEnumerable<Data.Player> players)
    {
        players ??= await _playerStore
            .List()
            .Where(p => p.TeamId == teamId)
            .ToListAsync();

        if (players.Count() == 0)
        {
            throw new CaptainResolutionFailure(teamId, "This team doesn't have any players.");
        }

        // if the team has a captain (manager), yay
        // if they have too many, boo (pick one by name which is stupid but stupid things happen sometimes)
        // if they don't have one, pick by name among all players
        var captains = players.Where(p => p.IsManager);

        if (captains.Count() == 1)
        {
            return captains.First();
        }
        else if (captains.Count() > 1)
        {
            return captains.OrderBy(c => c.ApprovedName).First();
        }

        return players.OrderBy(p => p.ApprovedName).First();
    }
}
