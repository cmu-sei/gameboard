using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gameboard.Api.Data;
using Gameboard.Api.Features.Challenges;
using Gameboard.Api.Features.Common;
using Gameboard.Api.Features.Games;
using Microsoft.EntityFrameworkCore;

namespace Gameboard.Api.Features.Reports;

public interface IEnrollmentReportService
{
    Task<IEnumerable<EnrollmentReportRecord>> GetRecords(EnrollmentReportQuery request, CancellationToken cancellationToken);
}

internal class EnrollmentReportService : IEnrollmentReportService
{
    private readonly IReportsService _reportsService;
    private readonly IStore _store;

    public EnrollmentReportService
    (
        IReportsService reportsService,
        IStore store
    )
    {
        _reportsService = reportsService;
        _store = store;
    }

    public async Task<IEnumerable<EnrollmentReportRecord>> GetRecords(EnrollmentReportQuery request, CancellationToken cancellationToken)
    {
        // parse multiselect criteria
        var seasonCriteria = _reportsService.ParseMultiSelectCriteria(request.Parameters.Seasons);
        var seriesCriteria = _reportsService.ParseMultiSelectCriteria(request.Parameters.Series);
        var sponsorCriteria = _reportsService.ParseMultiSelectCriteria(request.Parameters.Sponsors);
        var trackCriteria = _reportsService.ParseMultiSelectCriteria(request.Parameters.Tracks);

        // we also have to look up sponsors separately, because when we build the results, we have to translate
        // sponsor logos (which is what the Player entity has) to actual Sponsor entities
        var sponsors = await _store
            .List<Data.Sponsor>()
            .Select(s => new EnrollmentReportSponsorViewModel
            {
                Id = s.Id,
                Name = s.Name,
                LogoFileName = s.Logo
            })
            .ToArrayAsync(cancellationToken);

        // the fundamental unit of reporting here is really the player record (an "enrollment"), so resolve enrollments that
        // meet the filter criteria
        var query = _store
            .List<Data.Player>()
            .Include(p => p.Game)
            .Include(p => p.User)
            .Include(p => p.Challenges)
                .ThenInclude(c => c.AwardedManualBonuses)
            .Where(p => p.Game.PlayerMode == PlayerMode.Competition);

        if (seasonCriteria.Any())
            query = query.Where(p => seasonCriteria.Contains(p.Game.Season.ToLower()));

        if (seriesCriteria.Any())
            query = query.Where(p => seriesCriteria.Contains(p.Game.Competition.ToLower()));

        if (trackCriteria.Any())
            query = query.Where(p => trackCriteria.Contains(p.Game.Track.ToLower()));

        if (sponsorCriteria.Any())
        {
            // if sponsors have been specified as a criteria, we have to get a lookup, because the entities are not 
            // related by foreign key in the db
            var sponsorLogos = sponsors
                .Where(s => sponsorCriteria.Contains(s.Id))
                .Select(s => s.LogoFileName)
                .ToArray();

            query = query.Where(p => sponsorLogos.Contains(p.Sponsor));
        }

        // finalize query - we have to do the rest "client" (application server) side
        var players = await query.ToListAsync(cancellationToken);

        // This is pretty messy. Here's why:
        //
        // Teams are not first-class entities in the data model as of now. There's a teamId
        // on the player record which is always populated, even if the game is not a team game,
        // and there is another on the Challenge entity (which is also always populated). These
        // are not foreign keys and can't be the bases of join-like structures in EF.
        //
        // Additionally, the semantics of who owns a challenge vary between individual and team games.
        // As of now, when a team starts a challenge, a nearly-random (.First()) player is chosen and
        // assigned as the owner of the challenge. For the purposes of this report, this means that if
        // we strictly look at individual player registrations and report their challenges and performance,
        // we won't get the whole story if their challenges are owned by a teammate.
        //
        // To handle the fact that we conditionally need to report information about the team and may
        // need to report challenge data based on teammate rather than the player who represents the
        // current record, we grab team and challenge data for every player who met the criteria
        // above who is playing a team game (defined as a game with minimum team size > 1).
        var teamIds = players.Select(p => p.TeamId).ToArray();
        var teamAndChallengeData = await _store
            .List<Data.Player>()
            .Include(p => p.Challenges)
            .Include(p => p.Game)
            .Where(p => teamIds.Contains(p.TeamId))
            .Where(p => p.Challenges.Any())
            .Select(p => new
            {
                p.Id,
                p.TeamId,
                Name = p.ApprovedName,
                p.Role,
                p.Sponsor,
                Challenges = p.Challenges.Select(c => new EnrollmentReportChallengeQueryData
                {
                    SpecId = c.SpecId,
                    Name = c.Name,
                    WhenCreated = c.WhenCreated,
                    StartTime = c.StartTime,
                    EndTime = c.EndTime,
                    Score = c.Score,
                    Points = c.Points
                })
            })
            .GroupBy(p => p.TeamId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), cancellationToken);

        // transform the player records into enrollment report records
        var records = players.Select(p =>
        {
            var playerTeamChallengeData = teamAndChallengeData.ContainsKey(p.TeamId) ? teamAndChallengeData[p.TeamId] : null;
            var captain = playerTeamChallengeData?.FirstOrDefault(p => p.Role == PlayerRole.Manager);
            var playerTeamSponsorLogos = playerTeamChallengeData?.Select(p => p.Sponsor);
            var challenges = teamAndChallengeData[p.TeamId]
                .SelectMany(c => ChallengeDataToViewModel(c.Challenges))
                .DistinctBy(c => c.SpecId);

            return new EnrollmentReportRecord
            {
                Player = new SimpleEntity { Id = p.Id, Name = p.Name },
                Game = new EnrollmentReportGameViewModel
                {
                    Game = new SimpleEntity { Id = p.GameId, Name = p.Game.Name },
                    IsTeamGame = p.Game.MinTeamSize > 1
                },
                Team = p.Game.IsTeamGame() && playerTeamChallengeData != null ?
                    new EnrollmentReportTeamViewModel
                    {
                        Team = new SimpleEntity { Id = p.TeamId, Name = captain?.Name ?? p.Name },
                        CurrentCaptain = new SimpleEntity { Id = captain?.Id ?? p.Id, Name = captain?.Name ?? p.Name },
                        Sponsors = sponsors.Where(s => playerTeamSponsorLogos.Contains(s.LogoFileName)).ToArray()
                    }
                    : null,
                Session = new EnrollmentReportSessionViewModel
                {
                    SessionStart = p.SessionBegin.HasValue() ? p.SessionBegin : null,
                    SessionEnd = p.SessionEnd.HasValue() ? p.SessionEnd : null,
                    SessionLength = p.SessionBegin.HasValue() && p.SessionEnd.HasValue() ?
                        p.SessionEnd.Subtract(p.SessionBegin) :
                        null
                },
                Challenges = challenges ?? Array.Empty<EnrollmentReportChallengeViewModel>()
            };
        });

        return records;
    }

    private IEnumerable<EnrollmentReportChallengeViewModel> ChallengeDataToViewModel(IEnumerable<EnrollmentReportChallengeQueryData> challengeData)
        => challengeData.Select(c => new EnrollmentReportChallengeViewModel
        {
            Name = c.Name,
            SpecId = c.SpecId,
            DeployDate = c.WhenCreated,
            StartDate = c.StartTime,
            EndDate = c.EndTime,
            Duration = c.StartTime.HasValue() && c.EndTime.HasValue() ? c.EndTime.Subtract(c.StartTime) : null,
            Result = ChallengeExtensions.GetResult(c.Score, c.Points)
        });
}
