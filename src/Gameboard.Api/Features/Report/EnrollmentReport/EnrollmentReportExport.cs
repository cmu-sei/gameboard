using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace Gameboard.Api.Features.Reports;

public record EnrollmentReportExportQuery(EnrollmentReportParameters Parameters) : IRequest<IEnumerable<EnrollmentReportCsvRecord>>;

internal class EnrollmentReportExportHandler : IRequestHandler<EnrollmentReportExportQuery, IEnumerable<EnrollmentReportCsvRecord>>
{
    private readonly IEnrollmentReportService _enrollmentReportService;
    private readonly IMediator _mediator;

    public EnrollmentReportExportHandler(IMediator mediator, IEnrollmentReportService enrollmentReportService)
    {
        _enrollmentReportService = enrollmentReportService;
        _mediator = mediator;
    }

    public async Task<IEnumerable<EnrollmentReportCsvRecord>> Handle(EnrollmentReportExportQuery request, CancellationToken cancellationToken)
    {
        var results = await _enrollmentReportService.GetRecords(request.Parameters, cancellationToken);

        return results.Select(r => new EnrollmentReportCsvRecord
        {
            // user
            UserId = r.User.Id,
            UserName = r.User.Name,

            // player
            PlayerId = r.Player.Id,
            PlayerName = r.Player.Name,
            PlayerEnrollDate = r.Player.EnrollDate,
            PlayerSponsor = r.Player.Sponsor.Name,

            // game
            GameId = r.Game.Id,
            GameName = r.Game.Name,
            IsTeamGame = r.Game.IsTeamGame,
            Series = r.Game.Series,
            Season = r.Game.Season,
            Track = r.Game.Track,

            // session
            SessionStart = r.Session.Start,
            SessionEnd = r.Session.End,
            SessionDurationInSeconds = r.Session.DurationMs != null ? Math.Round(new Decimal((double)r.Session.DurationMs) / 1000, 2) : null,

            // team
            TeamId = r.Team?.Id,
            TeamName = r.Team?.Name,
            CaptainPlayerId = r.Team?.CurrentCaptain?.Id,
            CaptainPlayerName = r.Team?.CurrentCaptain?.Name,
            TeamSponsors = r.Team?.Sponsors?.Count() > 0 ? string.Join(", ", r.Team.Sponsors.Select(s => s.Name)) : null,

            // challenges
            Challenges = string.Join(", ", r.Challenges.Select(c => $"{c.Name} ({c.SpecId[..5]})")),
            FirstDeployDate = r.Challenges.Min(c => c.DeployDate),
            FirstStartDate = r.Challenges.Min(c => c.StartDate),
            LastEndDate = r.Challenges.Min(c => c.EndDate),
            MinDurationInSeconds = r.Challenges.All(c => c.DurationMs == null) ?
                null :
                Math.Round((double)r.Challenges.Min(c => c.DurationMs) / 1000, 2),
            MaxDurationInSeconds = r.Challenges.All(c => c.DurationMs == null) ?
                null :
                Math.Round((double)r.Challenges.Max(c => c.DurationMs) / 1000, 2),
            ChallengeScores = string.Join(",", r.Challenges.Select(c => $"{c.Score}/{c.MaxPossiblePoints}")),

            // challenge/game performance summary
            ChallengesPartiallySolvedCount = r.Challenges.Where(c => c.Result == ChallengeResult.Partial).Count(),
            ChallengesCompletelySolvedCount = r.Challenges.Where(c => c.Result == ChallengeResult.Success).Count(),
            Score = r.Score
        });
    }
}
