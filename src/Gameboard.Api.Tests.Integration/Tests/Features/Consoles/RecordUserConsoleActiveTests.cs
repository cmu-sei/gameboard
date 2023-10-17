using Gameboard.Api.Common;
using Gameboard.Api.Features.Consoles;
using Gameboard.Api.Features.Practice;

namespace Gameboard.Api.Tests.Integration;

[Collection(TestCollectionNames.DbFixtureTests)]
public class RecordUserConsoleActiveTests
{
    private readonly GameboardTestContext _testContext;

    public RecordUserConsoleActiveTests(GameboardTestContext testContext)
    {
        _testContext = testContext;
    }

    [Theory, GbIntegrationAutoData]
    public async Task ActionRecorded_WithPracticeSessionNearEnd_Extends(string userId, IFixture fixture)
    {
        // given
        await _testContext.WithDataState(state =>
        {
            state.Add<Data.Player>(fixture, p =>
            {
                p.SessionBegin = DateTimeOffset.UtcNow.AddMinutes(-10);
                p.SessionEnd = DateTimeOffset.UtcNow.AddMinutes(5);
                p.Sponsor = state.Build<Data.Sponsor>(fixture);
                p.Mode = PlayerMode.Practice;
                p.User = state.Build<Data.User>(fixture, u => u.Id = userId);
                p.Challenges = state.Build<Data.Challenge>(fixture, c =>
                {
                    c.PlayerMode = PlayerMode.Practice;
                }).ToCollection();
            });
        });

        // when
        var result = await _testContext
            .CreateHttpClientWithActingUser(u => u.Id = userId)
            .PostAsync("api/consoles/active", null)
            .WithContentDeserializedAs<ConsoleActionResponse>();

        // then
        // (See the source of RecordUserConsoleActive for a discussion about why this is currently a string)
        result.Message.ShouldBe(RecordUserConsoleActiveHandler.MESSAGE_EXTENDED);
    }
}
