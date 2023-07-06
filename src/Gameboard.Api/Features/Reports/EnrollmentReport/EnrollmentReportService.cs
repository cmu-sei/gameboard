using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gameboard.Api.Data;
using Gameboard.Api.Features.Challenges;
using Gameboard.Api.Common;
using Microsoft.EntityFrameworkCore;

namespace Gameboard.Api.Features.Reports;

public interface IEnrollmentReportService
{
    Task<IQueryable<Data.Player>> GetBaseQuery(EnrollmentReportParameters parameters, CancellationToken cancellationToken);
    Task<IEnumerable<EnrollmentReportRecord>> GetRecords(EnrollmentReportParameters parameters, CancellationToken cancellationToken);
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

    public async Task<IQueryable<Data.Player>> GetBaseQuery(EnrollmentReportParameters parameters, CancellationToken cancellationToken)
    {
        // parse multiselect criteria
        var seasonCriteria = _reportsService.ParseMultiSelectCriteria(parameters.Seasons);
        var seriesCriteria = _reportsService.ParseMultiSelectCriteria(parameters.Series);
        var sponsorCriteria = _reportsService.ParseMultiSelectCriteria(parameters.Sponsors);
        var trackCriteria = _reportsService.ParseMultiSelectCriteria(parameters.Tracks);

        // we have to look up sponsors to both resolve query criteria and to build the result set
        // (because the player entity has logo files, not ids)
        // doing this in both places for now because the cost is minimal and makes use of this query in the 
        // chart handler easier
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

        if (parameters.EnrollDateStart != null)
            query = query
                .WhereDateHasValue(p => p.WhenCreated)
                .Where(p => p.WhenCreated >= parameters.EnrollDateStart);

        if (parameters.EnrollDateEnd != null)
            query = query
                .WhereDateHasValue(p => p.WhenCreated)
                .Where(p => p.WhenCreated <= parameters.EnrollDateEnd);

        if (seasonCriteria.Any())
            query = query.Where(p => seasonCriteria.Contains(p.Game.Season.ToLower()));

        if (seriesCriteria.Any())
            query = query.Where(p => seriesCriteria.Contains(p.Game.Competition.ToLower()));

        if (trackCriteria.Any())
            query = query.Where(p => trackCriteria.Contains(p.Game.Track.ToLower()));

        if (sponsorCriteria.Any())
        {
            var sponsorLogos = sponsors
                .Where(s => sponsorCriteria.Contains(s.Id))
                .Select(s => s.LogoFileName)
                .ToArray();

            query = query.Where(p => sponsorLogos.Contains(p.Sponsor));
        }

        return query;
    }

    public async Task<IEnumerable<EnrollmentReportRecord>> GetRecords(EnrollmentReportParameters parameters, CancellationToken cancellationToken)
    {
        // load query
        var query = await GetBaseQuery(parameters, cancellationToken);

        // finalize query - we have to do the rest "client" (application server) side
        var players = await query.ToListAsync(cancellationToken);

        // look up sponsors to build the result set
        var sponsors = await _store
            .List<Data.Sponsor>()
            .Select(s => new EnrollmentReportSponsorViewModel
            {
                Id = s.Id,
                Name = s.Name,
                LogoFileName = s.Logo
            })
            .ToArrayAsync(cancellationToken);

        // This is pretty messy. Here's why:
        //
        // Teams are not first-class entities in the data model as of now. There's a teamId
        // on the player record which is always populated (even if the game is not a team game)
        // and there is another on the Challenge entity (which is also always populated). These
        // are not foreign keys and can't be the bases of join-like structures in EF.
        //
        // Additionally, the semantics of who owns a challenge vary between individual and team games.
        // As of now, when a team starts a challenge, a nearly-random (.First()) player is chosen and
        // assigned as the owner of the challenge. For the purposes of this report, this means that if
        // we strictly look at individual player registrations and report their challenges and performance,
        // we won't get the whole story if their challenges are owned by a teammate.
        //
        // To accommodate this, we just group all players by team id, create a dictionary of challenges
        // owned by any player on the team (by TeamId), and report the team's challenges for every player
        // on the team.
        var teamIds = players
            .Select(p => p.TeamId)
            .Distinct()
            .ToArray();

        var teamAndChallengeData = await _store
            .List<Data.Player>()
            .Include(p => p.Challenges)
            .Include(p => p.Game)
            .Include(p => p.User)
            .Where(p => teamIds.Contains(p.TeamId))
            .Select(p => new
            {
                p.Id,
                p.TeamId,
                Name = p.ApprovedName,
                p.Role,
                p.Score,
                p.Sponsor,
                Challenges = p.Challenges.Select(c => new EnrollmentReportChallengeQueryData
                {
                    SpecId = c.SpecId,
                    Name = c.Name,
                    WhenCreated = c.WhenCreated,
                    StartTime = c.StartTime,
                    EndTime = c.EndTime,
                    ManualChallengeBonuses = c.AwardedManualBonuses.Select(b => new EnrollmentReportManualChallengeBonus
                    {
                        Description = b.Description,
                        Points = b.PointValue
                    }),
                    Score = c.Score,
                    MaxPossiblePoints = c.Points
                })
            })
            .GroupBy(p => p.TeamId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), cancellationToken);

        // transform the player records into enrollment report records
        var records = players.Select(p =>
        {
            var playerTeamChallengeData = teamAndChallengeData[p.TeamId];
            var captain = playerTeamChallengeData.FirstOrDefault(p => p.Role == PlayerRole.Manager);
            var playerTeamSponsorLogos = playerTeamChallengeData.Select(p => p.Sponsor);
            var challenges = teamAndChallengeData[p.TeamId]
                .SelectMany(c => ChallengeDataToViewModel(c.Challenges))
                .DistinctBy(c => c.SpecId)
                .ToArray();

            return new EnrollmentReportRecord
            {
                User = new SimpleEntity { Id = p.UserId, Name = p.User.Name },
                Player = new EnrollmentReportPlayerViewModel
                {
                    Id = p.Id,
                    Name = p.ApprovedName,
                    EnrollDate = p.WhenCreated.HasValue() ? p.WhenCreated : null,
                    Sponsor = sponsors.FirstOrDefault(s => s.LogoFileName == p.Sponsor)
                },
                Game = new EnrollmentReportGameViewModel
                {
                    Id = p.GameId,
                    Name = p.Game.Name,
                    IsTeamGame = p.Game.MinTeamSize > 1,
                    Series = p.Game.Competition,
                    Season = p.Game.Season,
                    Track = p.Game.Track
                },
                Team = new EnrollmentReportTeamViewModel
                {
                    Id = p.TeamId,
                    Name = captain?.Name ?? p.Name,
                    CurrentCaptain = new SimpleEntity { Id = captain?.Id ?? p.Id, Name = captain?.Name ?? p.Name },
                    Sponsors = sponsors.Where(s => playerTeamSponsorLogos.Contains(s.LogoFileName)).ToArray()
                },
                PlayTime = new EnrollmentReportPlayTimeViewModel
                {
                    Start = p.SessionBegin.HasValue() ? p.SessionBegin : null,
                    DurationMs = p.Time > 0 ? p.Time : null,
                    End = (p.SessionBegin.HasValue() && p.Time > 0) ? p.SessionBegin.AddMilliseconds(p.Time) : null
                },
                Challenges = challenges,
                ChallengesPartiallySolvedCount = challenges.Where(c => c.Result == ChallengeResult.Partial).Count(),
                ChallengesCompletelySolvedCount = challenges.Where(c => c.Result == ChallengeResult.Success).Count(),
                Score = p.Score
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
            DurationMs = c.StartTime.HasValue() && c.EndTime.HasValue() ? c.EndTime.Subtract(c.StartTime).TotalMilliseconds : null,
            Result = ChallengeExtensions.GetResult(c.Score, c.MaxPossiblePoints),
            ManualChallengeBonuses = c.ManualChallengeBonuses,
            Score = c.Score,
            MaxPossiblePoints = c.MaxPossiblePoints
        });
}