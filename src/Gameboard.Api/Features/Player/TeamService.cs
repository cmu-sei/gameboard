using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Gameboard.Api.Data.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Gameboard.Api.Features.Player;

public interface ITeamService
{
    Task<Data.Player> ResolveCaptain(string teamId);
    Task PromoteCaptain(string teamId, string newCaptainPlayerId, User actingUser);
    Task UpdateTeamSponsors(string teamId);
}

internal class TeamService : ITeamService
{
    private readonly IMapper _mapper;
    private readonly IInternalHubBus _teamHubService;
    private readonly IPlayerStore _store;

    public TeamService(
        IMapper mapper,
        IInternalHubBus teamHubService,
        IPlayerStore store)
    {
        _mapper = mapper;
        _store = store;
        _teamHubService = teamHubService;
    }

    public async Task PromoteCaptain(string teamId, string newCaptainPlayerId, User actingUser)
    {
        var teamPlayers = await _store
            .List()
            .AsNoTracking()
            .Where(p => p.TeamId == teamId)
            .ToListAsync();

        var oldCaptain = teamPlayers.SingleOrDefault(p => p.Role == PlayerRole.Manager);
        var newCaptain = teamPlayers.Single(p => p.Id == newCaptainPlayerId);

        using (var transaction = await _store.DbContext.Database.BeginTransactionAsync())
        {
            await _store
                .List()
                .Where(p => p.TeamId == teamId)
                .ExecuteUpdateAsync(p => p.SetProperty(p => p.Role, p => PlayerRole.Member));

            var affectedPlayers = await _store
                .List()
                .Where(p => p.Id == newCaptainPlayerId)
                .ExecuteUpdateAsync
                (
                    p => p
                        .SetProperty(p => p.Role, p => PlayerRole.Manager)
                        .SetProperty(p => p.TeamSponsors, p => oldCaptain.TeamSponsors ?? p.TeamSponsors)
                );

            // this automatically rolls back the transaction
            if (affectedPlayers != 1)
                throw new PromotionFailed(teamId, newCaptainPlayerId, affectedPlayers);

            await UpdateTeamSponsors(teamId);

            await transaction.CommitAsync();
        }


        await _teamHubService.SendPlayerRoleChanged(_mapper.Map<Api.Player>(newCaptain), actingUser);
    }

    public async Task<Data.Player> ResolveCaptain(string teamId)
    {
        var players = await _store
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

    public async Task UpdateTeamSponsors(string teamId)
    {
        var members = await _store
            .List()
            .AsNoTracking()
            .Where(p => p.TeamId == teamId)
            .Select(p => new
            {
                Id = p.Id,
                Sponsor = p.Sponsor,
                IsManager = p.IsManager
            })
            .ToArrayAsync();

        if (members.Length == 0)
            return;

        var sponsors = string.Join('|', members
            .Select(p => p.Sponsor)
            .Distinct()
            .ToArray()
        );

        var manager = members.FirstOrDefault(p => p.IsManager);

        await _store
            .List()
            .Where(p => p.Id == manager.Id)
            .ExecuteUpdateAsync(p => p
                .SetProperty(p => p.TeamSponsors, sponsors));
    }
}
