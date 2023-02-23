using System.Net;
using Gameboard.Api;
using Gameboard.Api.Data;
using Gameboard.Api.Features.Player;
using Microsoft.EntityFrameworkCore;

namespace Gameboard.Tests.Integration;

public class PlayerControllerUnenrollTests : IClassFixture<GameboardTestContext<GameboardDbContextPostgreSQL>>
{
    private readonly GameboardTestContext<GameboardDbContextPostgreSQL> _testContext;

    public PlayerControllerUnenrollTests(GameboardTestContext<GameboardDbContextPostgreSQL> testContext)
    {
        _testContext = testContext;
    }

    [Theory, GbIntegrationAutoData]
    public async Task Unenroll_WhenIsMember_DeletesPlayerAndChallenges(IFixture fixture, string challengeId, string memberPlayerId, string memberUserId, string teamId)
    {
        // given
        await _testContext
            .WithDataState(state =>
            {
                state.AddGame(g =>
                {
                    g.Players = new Api.Data.Player[]
                    {
                        state.BuildPlayer(p =>
                        {
                            p.Id = fixture.Create<string>();
                            p.Name = "A";
                            p.TeamId = teamId;
                            p.Role = PlayerRole.Manager;
                        }),

                        state.BuildPlayer(p =>
                        {
                            p.Id = memberPlayerId;
                            p.Name = "B";
                            p.Role = PlayerRole.Member;
                            p.TeamId = teamId;
                            p.User = state.BuildUser(u =>
                            {
                                u.Id = memberUserId;
                                u.Role = UserRole.Member;
                            });
                            p.Challenges = new Api.Data.Challenge[]
                            {
                                state.BuildChallenge(c =>
                                {
                                    // the challenge is associated with the player but no other team
                                    // so it should get deleted
                                    c.Id = challengeId;
                                    c.PlayerId = memberPlayerId;
                                    c.TeamId = teamId;
                                })
                            };
                        })
                    };
                });
            });

        var httpClient = _testContext.CreateHttpClientWithAuth(u => u.Id = memberUserId);
        var reqParams = new PlayerUnenrollRequest
        {
            PlayerId = memberPlayerId
        };

        // when
        var response = await httpClient.DeleteAsync($"/api/player/{memberPlayerId}?{reqParams.ToQueryString()}");

        // then
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var hasPlayer = await _testContext.GetDbContext().Players.AnyAsync(p => p.Id == memberPlayerId);
        var hasChallenge = await _testContext.GetDbContext().Challenges.AnyAsync(c => c.Id == challengeId);

        hasPlayer.ShouldBeFalse();
        hasChallenge.ShouldBeFalse();
    }

    [Theory, GbIntegrationAutoData]
    public async Task Unenroll_WhenIsManager_Fails(string managerPlayerId, string managerUserId, string memberPlayerId)
    {
        // given
        await _testContext
            .WithDataState(state =>
            {
                state.AddGame(g =>
                {
                    g.Players = new Api.Data.Player[]
                    {
                        state.BuildPlayer(p =>
                        {
                            p.Id = managerPlayerId;
                            p.TeamId = "team";
                            p.Role = PlayerRole.Manager;
                            p.User = state.BuildUser(u =>
                            {
                                u.Id = managerUserId;
                                u.Role = UserRole.Member;
                            });
                        }),

                        state.BuildPlayer(p =>
                        {
                            p.Id = memberPlayerId;
                            p.Role = PlayerRole.Member;
                            p.TeamId = "team";
                        })
                    };
                });
            });

        var httpClient = _testContext.CreateHttpClientWithAuth(u => u.Id = managerUserId);
        var reqParams = new PlayerUnenrollRequest
        {
            PlayerId = managerPlayerId
        };

        // when / then
        var response = await httpClient.DeleteAsync($"/api/player/{managerPlayerId}?{reqParams.ToQueryString()}");

        // then
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
