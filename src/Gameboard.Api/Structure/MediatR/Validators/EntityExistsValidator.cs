using System;
using System.Threading.Tasks;
using Gameboard.Api.Data;
using Gameboard.Api.Data.Abstractions;

namespace Gameboard.Api.Structure.MediatR.Validators;

internal class EntityExistsValidator<TModel, TEntity> : IGameboardValidator<TModel>
    where TEntity : class, IEntity
{
    private readonly IStore<TEntity> _store;
    private Func<TModel, string> _idProperty;

    public EntityExistsValidator(IStore<TEntity> store)
    {
        _store = store;
    }

    public async Task<GameboardValidationException> Validate(TModel model)
    {
        var id = _idProperty(model);
        if (!(await _store.Exists(id)))
            return new ResourceNotFound<TEntity>(id);

        return null;
    }

    public EntityExistsValidator<TModel, TEntity> UseProperty(Func<TModel, string> idProperty)
    {
        _idProperty = idProperty;
        return this;
    }
}