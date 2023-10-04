using System;
using System.Linq;
using System.Threading.Tasks;
using Gameboard.Api.Data;
using Gameboard.Api.Structure.MediatR;
using Gameboard.Api.Structure.MediatR.Validators;
using Microsoft.EntityFrameworkCore;

namespace Gameboard.Api.Features.Games.Validators;

public class UserIsPlayingGameValidator : IGameboardValidator
{
    private string _gameId;
    private string _userId;
    private readonly IStore _store;

    public UserIsPlayingGameValidator(IStore store) => _store = store;

    public UserIsPlayingGameValidator UseGameId(string gameId)
    {
        _gameId = gameId;
        return this;
    }

    public UserIsPlayingGameValidator UseUserId(string userId)
    {
        _userId = userId;
        return this;
    }

    public Func<RequestValidationContext, Task> GetValidationTask()
    {
        return async context =>
        {
            var user = await _store
                .WithNoTracking<Data.User>()
                .FirstOrDefaultAsync(u => u.Id == _userId);

            if (user.Role.HasFlag(UserRole.Admin) || user.Role.HasFlag(UserRole.Tester))
                return;

            var hasPlayer = await _store
                .WithNoTracking<Data.Player>()
                .AnyAsync(p => p.UserId == _userId && p.GameId == _gameId);

            if (!hasPlayer)
                context.AddValidationException(new UserIsntPlayingGame(_userId, _gameId));
        };
    }
}


public class UserIsPlayingGameValidator<T> : IGameboardValidator<T> where T : class
{
    private Func<T, string> _gameIdExpression;
    private Func<T, User> _userExpression;
    private readonly IStore _store;

    public UserIsPlayingGameValidator(IStore store) => _store = store;

    public UserIsPlayingGameValidator<T> UseGameIdProperty(Func<T, string> gameIdPropertyExpression)
    {
        _gameIdExpression = gameIdPropertyExpression;
        return this;
    }

    public UserIsPlayingGameValidator<T> UseUserIdProperty(Func<T, User> userPropertyExpression)
    {
        _userExpression = userPropertyExpression;
        return this;
    }

    public Func<T, RequestValidationContext, Task> GetValidationTask()
    {
        return async (model, context) =>
        {
            var user = _userExpression(model);
            var gameId = _gameIdExpression(model);

            if (user.Role.HasFlag(UserRole.Admin) || user.Role.HasFlag(UserRole.Tester))
                return;

            var hasPlayer = await _store
                .WithNoTracking<Data.Player>()
                .Where(p => p.UserId == user.Id && p.GameId == gameId)
                .AnyAsync();

            if (!hasPlayer)
                context.AddValidationException(new UserIsntPlayingGame(user.Id, gameId, "User must be playing a game in order to read its sync start state."));
        };
    }
}
