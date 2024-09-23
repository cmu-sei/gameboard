// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gameboard.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gameboard.Api.Validators
{
    public class FeedbackValidator(IStore store) : IModelValidator
    {
        private readonly IStore _store = store;

        public Task Validate(object model)
        {
            if (model is FeedbackSubmission)
                return _validate(model as FeedbackSubmission);

            throw new System.NotImplementedException();
        }

        private async Task _validate(FeedbackSubmission model)
        {
            if (!await _store.AnyAsync<Data.Game>(g => g.Id == model.GameId, CancellationToken.None))
                throw new ResourceNotFound<Data.Game>(model.GameId);

            if (model.ChallengeId.IsEmpty() != model.ChallengeSpecId.IsEmpty()) // must specify both or neither
                throw new InvalideFeedbackFormat();

            // if not blank, must exist for challenge and challenge spec
            if (model.ChallengeSpecId.IsEmpty())
                throw new ArgumentException("ChallengeSpecId is required");

            if (!await _store.AnyAsync<Data.ChallengeSpec>(s => s.Id == model.ChallengeSpecId, CancellationToken.None))
                throw new ResourceNotFound<ChallengeSpec>(model.ChallengeSpecId);

            if (!await _store.AnyAsync<Data.Challenge>(c => c.Id == model.ChallengeId, CancellationToken.None))
                throw new ResourceNotFound<Challenge>(model.ChallengeSpecId);

            // if specified, this is a challenge-specific feedback response, so validate challenge/spec/game match
            if (model.ChallengeSpecId.NotEmpty())
            {
                if (!await _store.AnyAsync<Data.ChallengeSpec>(s => s.Id == model.ChallengeSpecId && s.GameId == model.GameId, CancellationToken.None))
                    throw new ActionForbidden();

                if (!await _store.AnyAsync<Data.Challenge>(c => c.Id == model.ChallengeId && c.SpecId == model.ChallengeSpecId, CancellationToken.None))
                    throw new ActionForbidden();
            }

            await Task.CompletedTask;
        }
    }
}
