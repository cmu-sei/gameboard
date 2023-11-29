using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace Gameboard.Api.Features.Reports;

public record EnrollmentReportByGameQuery(EnrollmentReportParameters Parameters, PagingArgs PagingArgs, User ActingUser) : IRequest<ReportResults<EnrollmentReportByGameRecord>>, IReportQuery;

internal class EnrollmentReportByGameHandler : IRequestHandler<EnrollmentReportByGameQuery, ReportResults<EnrollmentReportByGameRecord>>
{
    private readonly IEnrollmentReportService _enrollmentReportService;
    private readonly ReportsQueryValidator _queryValidator;
    private readonly IReportsService _reportsService;

    public EnrollmentReportByGameHandler
    (
        IEnrollmentReportService enrollmentReportService,
        ReportsQueryValidator queryValidator,
        IReportsService reportsService
    )
    {
        _enrollmentReportService = enrollmentReportService;
        _queryValidator = queryValidator;
        _reportsService = reportsService;
    }

    public async Task<ReportResults<EnrollmentReportByGameRecord>> Handle(EnrollmentReportByGameQuery request, CancellationToken cancellationToken)
    {
        // validate
        await _queryValidator.Validate(request, cancellationToken);

        // get the dataset, slim it down and group it
        var rawResults = await _enrollmentReportService
            .GetBaseQuery(request.Parameters)
            .Select(p => new
            {
                Game = new EnrollmentReportByGameGame
                {
                    Id = p.GameId,
                    Name = p.Game.Name,
                    Season = p.Game.Season,
                    Series = p.Game.Division,
                    Track = p.Game.Track,
                    IsTeamGame = p.Game.MaxTeamSize > 1,
                    ExecutionClosed = p.Game.GameEnd,
                    ExecutionOpen = p.Game.GameStart,
                    RegistrationOpen = p.Game.RegistrationOpen,
                    RegistrationClosed = p.Game.RegistrationClose
                },
                Sponsor = new ReportSponsorViewModel
                {
                    Id = p.SponsorId,
                    Name = p.Sponsor.Name,
                    LogoFileName = p.Sponsor.Logo
                },
                PlayerId = p.Id,
                p.UserId,
            })
            .ToArrayAsync(cancellationToken);

        // now do the stuff we can't easily translate to the db level
        var allSponsors = rawResults
            .Select(p => p.Sponsor)
            .GroupBy(s => s.Id)
            .ToDictionary(s => s.Key, s =>
            {
                var first = s.First();

                return new EnrollmentReportByGameSponsor
                {
                    Id = s.Key,
                    Name = first.Name,
                    LogoFileName = first.LogoFileName,
                    PlayerCount = s.Count()
                };
            });

        var gamePlayerCount = rawResults
            .GroupBy(g => g.Game.Id)
            .ToDictionary(gr => gr.Key, gr => gr.DistinctBy(p => p.UserId).Count());

        var gameSponsorPlayerCount = rawResults
            .GroupBy(g => new { GameId = g.Game.Id, SponsorId = g.Sponsor.Id })
            .Select(gr => new
            {
                gr.Key.GameId,
                gr.Key.SponsorId,
                PlayerCount = gr.Count()
            })
            .OrderBy(entry => entry.GameId)
                .ThenByDescending(entry => entry.PlayerCount)
            .ToArray();

        var groupedResults = rawResults
            .GroupBy(p => p.Game.Id)
            .Select(gr =>
            {
                // all the game info should be the same, we just need
                // a single instance to return the gameinfo
                var gameInfo = gr.First();
                var topSponsorId = gameSponsorPlayerCount
                    .Where(c => c.GameId == gr.Key)
                    .OrderByDescending(c => c.PlayerCount)
                    .Select(c => c.SponsorId)
                    .FirstOrDefault();

                return new EnrollmentReportByGameRecord
                {
                    Game = gameInfo.Game,
                    PlayerCount = gamePlayerCount[gr.Key],
                    Sponsors = gr
                        .Select(entry => entry.Sponsor)
                        .DistinctBy(s => s.Id)
                        .Select(s => allSponsors[s.Id]),
                    TopSponsor = allSponsors[topSponsorId]
                };
            })
            .ToArray();

        return _reportsService.BuildResults(new ReportRawResults<EnrollmentReportByGameRecord>
        {
            ParameterSummary = string.Empty,
            PagingArgs = request.PagingArgs,
            Records = groupedResults,
            ReportKey = ReportKey.Enrollment,
            Title = "Enrollment Report (Grouped By Game)"
        });
    }
}
