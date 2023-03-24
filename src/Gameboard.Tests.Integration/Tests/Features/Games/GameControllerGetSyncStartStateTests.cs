using Gameboard.Api.Data;
using Gameboard.Api.Features.Games;

namespace Gameboard.Tests.Integration;

public class GameControllerGetSyncStartStateTests : IClassFixture<GameboardTestContext<GameboardDbContextPostgreSQL>>
{
    private readonly GameboardTestContext<GameboardDbContextPostgreSQL> _testContext;

    public GameControllerGetSyncStartStateTests(GameboardTestContext<GameboardDbContextPostgreSQL> testContext)
    {
        _testContext = testContext;
    }

    [Theory, GbIntegrationAutoData]
    public async Task GetSyncStartState_WithAllReady_IsReady(string gameId)
    {
        // given two players registered for the same sync-start game, both ready
        await _testContext.WithDataState(state =>
        {
            state.AddGame(g =>
            {
                g.Id = gameId;
                g.RequireSynchronizedStart = true;
            });

            state.AddPlayer(p =>
            {
                p.GameId = gameId;
                p.IsReady = true;
            });

            state.AddPlayer(p =>
            {
                p.GameId = gameId;
                p.IsReady = true;
            });
        });

        var http = _testContext.CreateDefaultClient();

        // when
        var result = await http
            .GetAsync($"/api/game/{gameId}/ready")
            .WithContentDeserializedAs<SyncStartState>();

        // then
        result.ShouldNotBeNull();
        result.IsSyncStartReady.ShouldBeTrue();
        result.Teams.Count().ShouldBe(2);
    }

    [Theory, GbIntegrationAutoData]
    public async Task GetSyncStartState_WithNotReady_IsNotReady(string gameId, string notReadyPlayerId)
    {
        // given two players registered for the same sync-start game, one not ready
        await _testContext.WithDataState(state =>
        {
            state.AddGame(g =>
            {
                g.Id = gameId;
                g.RequireSynchronizedStart = true;
            });

            state.AddPlayer(p =>
            {
                p.GameId = gameId;
                p.IsReady = true;
            });

            state.AddPlayer(p =>
            {
                p.Id = notReadyPlayerId;
                p.GameId = gameId;
                p.IsReady = false;
            });
        });

        var http = _testContext.CreateDefaultClient();

        // when 
        var result = await http
            .GetAsync($"/api/game/{gameId}/ready")
            .WithContentDeserializedAs<SyncStartState>();

        // then
        result.ShouldNotBeNull();
        result.IsSyncStartReady.ShouldBeFalse();
        result.Teams.Count().ShouldBe(2);
        result
            .Teams
            .Where(t => !t.IsReady)
            .Single()
            .Players
            .Any(p => p.Id == notReadyPlayerId)
            .ShouldBeTrue();
    }

    [Theory, GbIntegrationAutoData]
    public async Task GetSyncStartState_WithNotRequiredSyncStart_IsReady(string gameId)
    {
        // given a player registered for a game which doesn't require sync start
        await _testContext.WithDataState(state =>
        {
            state.AddGame(g =>
            {
                g.Id = gameId;
                g.RequireSynchronizedStart = false;
            });

            state.AddPlayer(p =>
            {
                p.GameId = gameId;
                p.IsReady = false;
            });
        });

        var http = _testContext.CreateDefaultClient();

        // when
        var result = await http
            .GetAsync($"/api/game/{gameId}/ready")
            .WithContentDeserializedAs<SyncStartState>();

        // then
        result.ShouldNotBeNull();
        result.IsSyncStartReady.ShouldBeTrue();
        result.Teams.Count().ShouldBe(0);
    }
}
