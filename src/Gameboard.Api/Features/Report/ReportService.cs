using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Gameboard.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TopoMojo.Api.Client;

namespace Gameboard.Api.Services
{
    public class ReportService : _Service
    {
        GameboardDbContext Store { get; }
        ChallengeService _challengeService { get; }

        public ReportService (
            ILogger<ReportService> logger,
            IMapper mapper,
            CoreOptions options,
            GameboardDbContext store,
            ChallengeService challengeService
        ): base (logger, mapper, options)
        {
            Store = store;
            _challengeService = challengeService;
        }

        internal Task<UserReport> GetUserStats()
        {
            UserReport userReport = new UserReport
            {
                Timestamp = DateTime.UtcNow,
                EnrolledUserCount = Store.Users.Where(u => u.Enrollments.Count() > 0).Count(),
                UnenrolledUserCount = Store.Users.Where(u => u.Enrollments.Count == 0).Count(),
            };

            return Task.FromResult(userReport);
        }

        internal Task<PlayerReport> GetPlayerStats()
        {
            var ps = from games in Store.Games
                     select new PlayerStat { GameId = games.Id, GameName = games.Name, PlayerCount = games.Players.Count };

            PlayerReport playerReport = new PlayerReport
            {
                Timestamp = DateTime.UtcNow,
                Stats = ps.ToArray()
            };

            return Task.FromResult(playerReport);
        }

        internal Task<SponsorReport> GetSponsorStats()
        {
            var sp = (from sponsors in Store.Sponsors
                      join u in Store.Users on
                      sponsors.Logo equals u.Sponsor
                      select new { sponsors.Id, sponsors.Name, sponsors.Logo }).GroupBy(s => new { s.Id, s.Name, s.Logo })
                      .Select(g => new SponsorStat { Id = g.Key.Id, Name = g.Key.Name, Logo = g.Key.Logo, Count = g.Count() }).OrderByDescending(g => g.Count).ThenBy(g => g.Name);

            SponsorReport sponsorReport = new SponsorReport
            {
                Timestamp = DateTime.UtcNow,
                Stats = sp.ToArray()
            };

            return Task.FromResult(sponsorReport);
        }

        internal Task<GameSponsorReport> GetGameSponsorsStats(string gameId)
        {
            List<GameSponsorStat> gameSponsorStats = new List<GameSponsorStat>();

            if (string.IsNullOrWhiteSpace(gameId))
            {
                throw new ArgumentNullException("Invalid game id");
            }

            var game = Store.Games.Where(g => g.Id == gameId).Select(g => new { g.Id, g.Name }).FirstOrDefault();

            if (game == null)
            {
                throw new Exception("Invalid game");
            }

            var players = Store.Players.Where(p => p.GameId == gameId)
                .Select(p => new { p.Sponsor, p.TeamId }).ToList();

            var sponsors = Store.Sponsors;

            List<SponsorStat> sponsorStats = new List<SponsorStat>();

            foreach (Data.Sponsor sponsor in sponsors)
            {
                sponsorStats.Add(new SponsorStat
                {
                    Id = sponsor.Id,
                    Name = sponsor.Name,
                    Logo = sponsor.Logo,
                    Count = players.Where(p => p.Sponsor == sponsor.Logo).Count(),
                    TeamCount = players.Where(p => p.Sponsor == sponsor.Logo).Select(p => p.TeamId).Distinct().Count()
                });
            }

            GameSponsorStat gameSponsorStat = new GameSponsorStat
            {
                GameId = gameId,
                GameName = game.Name,
                Stats = sponsorStats.ToArray()
            };

            gameSponsorStats.Add(gameSponsorStat);

            GameSponsorReport sponsorReport = new GameSponsorReport
            {
                Timestamp = DateTime.UtcNow,
                Stats = gameSponsorStats.ToArray()
            };

            return Task.FromResult(sponsorReport);
        }

        internal async Task<ChallengeReport> GetChallengeStats(string gameId)
        {
            var challenges = await Store.Challenges
                .Where(c => c.GameId == gameId)
                .Select(c => new {
                    SpecId = c.SpecId,
                    Name = c.Name,
                    Tag = c.Tag,
                    Points = c.Points,
                    Score = c.Score,
                    Result = c.Result,
                    Duration = c.Duration
                })
                .ToArrayAsync()
            ;

            var stats = challenges
                .GroupBy(c => c.SpecId)
                .Select(g => new ChallengeStat
                {
                    Id = g.Key,
                    Name = g.First().Name,
                    Tag = g.First().Tag,
                    Points = g.First().Points,
                    SuccessCount = g.Count(o => o.Result == ChallengeResult.Success),
                    PartialCount = g.Count(o => o.Result == ChallengeResult.Partial),
                    AverageTime = g.Any(c => c.Result == ChallengeResult.Success)
                        ? new TimeSpan(0, 0, 0, 0, (int) g
                            .Where(c => c.Result == ChallengeResult.Success)
                            .Average(o => o.Duration)
                        ).ToString(@"hh\:mm\:ss")
                        : "",
                    AttemptCount = g.Count(),
                    AverageScore = (int)g.Average(c => c.Score)
                })
                .ToArray()
            ;

            ChallengeReport challengeReport = new ChallengeReport
            {
                Timestamp = DateTime.UtcNow,
                Stats = stats.ToArray()
            };

            return challengeReport;
        }

        internal async Task<ChallengeDetailReport> GetChallengeDetails(string id)
        {
            var challenges = Mapper.Map<Challenge[]>(await Store.Challenges.Where(c => c.SpecId == id).ToArrayAsync());
            List<Part> parts = new List<Part>();

            if (challenges.Length > 0)
            {
                QuestionView[] questions = challenges[0].State.Challenge.Questions.ToArray();

                foreach (QuestionView questionView in questions)
                {
                    parts.Add(new Part{ Text = questionView.Text, SolveCount = 0, AttemptCount = 0 });
                }

                foreach (Challenge challenge in challenges)
                {
                    foreach (QuestionView questionView in challenge.State.Challenge.Questions)
                    {
                        if (questionView.IsGraded)
                        {
                            Part part = parts.Find(p => p.Text == questionView.Text);

                            if (part != null)
                            {
                                if (questionView.IsCorrect)
                                {
                                    part.SolveCount++;
                                }
                            }
                        }
                    }
                }
            }

            ChallengeDetailReport challengeDetailReport = new ChallengeDetailReport();
            challengeDetailReport.Timestamp = DateTime.UtcNow;
            challengeDetailReport.Parts = parts.ToArray();
            challengeDetailReport.AttemptCount = challenges != null ? challenges.Length : 0;
            challengeDetailReport.ChallengeId = id;

            return challengeDetailReport;
        }
    }
}