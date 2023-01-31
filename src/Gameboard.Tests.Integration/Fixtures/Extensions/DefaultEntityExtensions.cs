using Gameboard.Api.Data;

namespace Gameboard.Tests.Integration.Fixtures;

public static class GameboardTestContextDefaultEntityExtensions
{
    private static T BuildEntity<T>(T entity, Action<T>? builder = null, Action<T>? cleanUp = null) where T : class, IEntity
    {
        builder?.Invoke(entity);
        cleanUp?.Invoke(entity);
        return entity;
    }

    public static void AddChallengeSpec(this IDataStateBuilder dataStateBuilder, Action<ChallengeSpec>? specBuilder = null)
        => dataStateBuilder.Add(BuildChallengeSpec(dataStateBuilder, specBuilder));

    public static ChallengeSpec BuildChallengeSpec(this IDataStateBuilder dataStateBuilder, Action<ChallengeSpec>? specBuilder = null)
        => BuildEntity
        (
            new ChallengeSpec
            {
                Id = TestIds.Generate(),
                Game = BuildGame(dataStateBuilder),
                Name = "Integration Test Challenge Spec",
                AverageDeploySeconds = 1,
                Points = 50,
                X = 0,
                Y = 0,
                R = 1
            },
            specBuilder
        );

    public static void AddGame(this IDataStateBuilder dataStateBuilder, Action<Game>? gameBuilder = null)
        => dataStateBuilder.Add(BuildGame(dataStateBuilder, gameBuilder));

    public static Game BuildGame(this IDataStateBuilder dataStateBuilder, Action<Game>? gameBuilder = null)
        => BuildEntity
        (
            new Game
            {
                Id = TestIds.Generate(),
                Name = "Test game",
                Competition = "Test competition",
                Season = "1",
                Track = "Individual",
                Sponsor = "Test Sponsor",
                GameStart = DateTimeOffset.UtcNow,
                GameEnd = DateTime.UtcNow + TimeSpan.FromDays(30),
                RegistrationOpen = DateTimeOffset.UtcNow,
                RegistrationClose = DateTime.UtcNow + TimeSpan.FromDays(30),
                RegistrationType = Gameboard.Api.GameRegistrationType.Open,
            },
            gameBuilder
        );

    public static void AddPlayer(this IDataStateBuilder dataStateBuilder, Action<Player>? playerBuilder = null)
        => dataStateBuilder.Add(BuildPlayer(dataStateBuilder, playerBuilder));

    public static Player BuildPlayer(this IDataStateBuilder dataStateBuilder, Action<Player>? playerBuilder = null)
    {
        // TODO: this is potentially urky if the testing dev sets userid but not user's id
        var userId = TestIds.Generate();

        return BuildEntity
        (
            new Player
            {
                Id = TestIds.Generate(),
                TeamId = TestIds.Generate(),
                ApprovedName = "Integration Test Player",
                Sponsor = "Integration Test Sponsor",
                Role = Gameboard.Api.PlayerRole.Manager,
                User = new User { Id = userId },
                UserId = userId
            },
            playerBuilder
        );
    }

    public static void AddUser(this IDataStateBuilder dataStateBuilder, Action<User>? userBuilder = null)
        => dataStateBuilder.Add(BuildUser(dataStateBuilder, userBuilder));

    public static User BuildUser(this IDataStateBuilder dataStateBuilder, Action<User>? userBuilder = null)
        => BuildEntity
        (
            new Api.Data.User
            {
                Id = TestIds.Generate(),
                Username = "integrationtester",
                Email = "integration@test.com",
                Name = "integrationtester",
                ApprovedName = "integrationtester",
                Sponsor = "SEI",
                Role = Api.UserRole.Member
            },
            userBuilder
        );

    public static IEnumerable<Player> BuildTeam(this IDataStateBuilder builder, int teamSize = 5, Action<Player>? playerBuilder = null)
    {
        var team = new List<Player>();
        var teamId = TestIds.Generate();

        for (var i = 0; i < teamSize; i++)
        {
            var player = BuildPlayer(builder, p => p.TeamId = teamId);
            playerBuilder?.Invoke(player);
            team.Add(player);
        }

        return team;
    }
}