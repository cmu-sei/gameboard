using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Gameboard.Api.Common.Services;
using Gameboard.Api.Data;
using Gameboard.Api.Structure.MediatR;
using Gameboard.Api.Structure.MediatR.Validators;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Gameboard.Api.Features.Scores;

public record UpdateTeamChallengeBaseScoreCommand(string ChallengeId, double Score) : IRequest<TeamChallengeScore>;

internal class UpdateTeamChallengeBaseScoreHandler : IRequestHandler<UpdateTeamChallengeBaseScoreCommand, TeamChallengeScore>
{
    private readonly EntityExistsValidator<UpdateTeamChallengeBaseScoreCommand, Data.Challenge> _challengeExists;
    private readonly IGuidService _guidService;
    private readonly IMapper _mapper;
    private readonly IScoringService _scoringService;
    private readonly IStore _store;
    private readonly IValidatorService<UpdateTeamChallengeBaseScoreCommand> _validator;
    private readonly GameboardDbContext _dbContext;

    public UpdateTeamChallengeBaseScoreHandler
    (
        EntityExistsValidator<UpdateTeamChallengeBaseScoreCommand, Data.Challenge> challengeExists,
        IGuidService guidService,
        IMapper mapper,
        IScoringService scoringService,
        IStore store,
        IValidatorService<UpdateTeamChallengeBaseScoreCommand> validator,
        GameboardDbContext dbContext
    )
    {
        _challengeExists = challengeExists;
        _guidService = guidService;
        _mapper = mapper;
        _scoringService = scoringService;
        _store = store;
        _validator = validator;
        _dbContext = dbContext;
    }

    public async Task<TeamChallengeScore> Handle(UpdateTeamChallengeBaseScoreCommand request, CancellationToken cancellationToken)
    {
        // validate
        var challenge = await _store
            .WithNoTracking<Data.Challenge>()
            .Include(c => c.Game)
            .Include(c => c.AwardedBonuses)
                .ThenInclude(b => b.ChallengeBonus)
            .FirstOrDefaultAsync(c => c.Id == request.ChallengeId, cancellationToken);

        // TODO: validator may need to be able to one-off validate, because all of this will fail without a challenge id
        _validator.AddValidator(_challengeExists.UseProperty(c => c.ChallengeId));
        _validator.AddValidator
        (
            (req, context) =>
            {
                if (request.Score < 0)
                    context.AddValidationException(new CantAwardNegativePointValue(challenge.Id, challenge.TeamId, request.Score));
            }
        );

        _validator.AddValidator
        (
            async (req, context) =>
            {
                if (!await _store.WithNoTracking<Data.ChallengeSpec>().AnyAsync(s => s.Id == challenge.SpecId))
                    context.AddValidationException(new ResourceNotFound<Data.ChallengeSpec>(challenge.SpecId));
            }
        );

        // can't change the team's score if they've already received a bonus
        if (challenge.Score > 0)
        {
            _validator.AddValidator
            (
                (req, context) =>
                {
                    var awardedBonus = challenge.AwardedBonuses.FirstOrDefault(b => b.ChallengeBonus.PointValue > 0);

                    if (challenge.AwardedBonuses.Any(b => b.ChallengeBonus.PointValue > 0))
                        context.AddValidationException(new CantRescoreChallengeWithANonZeroBonus
                        (
                            request.ChallengeId,
                            challenge.TeamId,
                            awardedBonus.Id,
                            awardedBonus.ChallengeBonus.PointValue
                        ));

                    return Task.CompletedTask;
                }
            );
        }

        await _validator.Validate(request, cancellationToken);

        // load additional data
        // note: right now, we're only awarding solve rank bonuses right now
        var spec = await _store
            .WithNoTracking<Data.ChallengeSpec>()
            .Include
            (
                spec => spec
                    .Bonuses
                    .Where(b => b.ChallengeBonusType == ChallengeBonusType.CompleteSolveRank)
                    .OrderBy(b => (b as ChallengeBonusCompleteSolveRank).SolveRank)
            ).FirstAsync(spec => spec.Id == challenge.SpecId, cancellationToken);

        // other copies of this challenge for other teams who have a solve
        var otherTeamChallenges = await _store
            .WithNoTracking<Data.Challenge>()
            .Include(c => c.AwardedBonuses)
            .Where(c => c.SpecId == spec.Id)
            .Where(c => c.TeamId != challenge.TeamId)
            .WhereIsFullySolved()
            // end time of the challenge against game start to get ranks
            .OrderBy(c => c.EndTime - challenge.Game.GameStart)
            .ToArrayAsync(cancellationToken);

        // award points
        // treating this as a dbcontext savechanges to preserve atomicity
        var updateChallenge = await _dbContext.Challenges.FirstAsync(c => c.Id == request.ChallengeId, cancellationToken);
        updateChallenge.Score = request.Score;

        // if they have a full solve, compute their ordinal rank by time and award them the appropriate challenge bonus
        if (challenge.Result == ChallengeResult.Success)
        {
            var availableBonuses = spec
                .Bonuses
                .Where(bonus => !otherTeamChallenges.SelectMany(c => c.AwardedBonuses).Any(otherTeamBonus => otherTeamBonus.Id == bonus.Id));

            if (availableBonuses.Any() && (availableBonuses.First() as ChallengeBonusCompleteSolveRank).SolveRank == otherTeamChallenges.Length + 1)
                updateChallenge.AwardedBonuses.Add(new AwardedChallengeBonus
                {
                    Id = _guidService.GetGuid(),
                    ChallengeBonusId = availableBonuses.First().Id
                });
        }

        // commit it
        await _dbContext.SaveChangesAsync(cancellationToken);

        // query manual bonuses to compose a complete score
        return _mapper.Map<TeamChallengeScore>(await _scoringService.GetTeamChallengeScore(challenge.Id));
    }
}