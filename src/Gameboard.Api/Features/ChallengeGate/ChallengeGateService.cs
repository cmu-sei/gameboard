// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Gameboard.Api.Data.Abstractions;
using Microsoft.Extensions.Logging;

namespace Gameboard.Api.Services
{
    public class ChallengeGateService(
        ILogger<ChallengeGateService> logger,
        IMapper mapper,
        CoreOptions options,
        IStore<Data.ChallengeGate> store
        ) : _Service(logger, mapper, options)
    {
        IStore<Data.ChallengeGate> Store { get; } = store;

        public async Task<ChallengeGate> AddOrUpdate(NewChallengeGate model)
        {
            var entity = await Store.List().FirstOrDefaultAsync(s =>
                s.TargetId == model.TargetId &&
                s.RequiredId == model.RequiredId &&
                s.GameId == model.GameId
            );

            if (entity is not null)
            {
                Mapper.Map(model, entity);
                await Store.Update(entity);
            }
            else
            {
                entity = Mapper.Map<Data.ChallengeGate>(model);
                await Store.Create(entity);
            }

            return Mapper.Map<ChallengeGate>(entity);
        }

        public async Task<ChallengeGate> Retrieve(string id)
        {
            return Mapper.Map<ChallengeGate>(await Store.Retrieve(id));
        }

        public async Task Update(ChangedChallengeGate account)
        {
            var entity = await Store.Retrieve(account.Id);

            Mapper.Map(account, entity);

            await Store.Update(entity);
        }

        public Task Delete(string id)
            => Store.Delete(id);

        internal async Task<ChallengeGate[]> List(string id)
        {
            if (id.IsEmpty())
                return [];

            return
                await Mapper.ProjectTo<ChallengeGate>(
                    Store.List().Where(g => g.GameId == id)
                ).ToArrayAsync()
            ;
        }
    }

}
