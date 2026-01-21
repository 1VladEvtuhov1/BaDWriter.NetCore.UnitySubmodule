// Path: Database.Application/Cards/CardQueryService.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Cards;
using Database.Abstractions.Queries;
using Database.Application.Queries;

namespace Database.Application.Cards
{
    public sealed class CardQueryService : ICardQueryService
    {
        private readonly ICardQueries _q;
        private readonly ICardVariantQueries _vq;

        public CardQueryService(ICardQueries q, ICardVariantQueries vq)
        {
            _q = q ?? throw new ArgumentNullException(nameof(q));
            _vq = vq ?? throw new ArgumentNullException(nameof(vq));
        }

        public CardQueryService(ICardQueries q)
        {
            _q = q ?? throw new ArgumentNullException(nameof(q));
            _vq = q as ICardVariantQueries
                  ?? throw new InvalidOperationException(
                      "ICardVariantQueries is not available from the injected repository. " +
                      "Register ICardVariantQueries or use the (ICardQueries, ICardVariantQueries) constructor.");
        }

        public Task<CardDto> GetAsync(string id, CancellationToken ct) => _q.GetAsync(id, ct);

        public IAsyncEnumerable<CardDto> ListByCardContainerAsync(string parentId, int skip, int take, CancellationToken ct)
            => _q.ListByCardContainerAsync(parentId, skip, take, ct);

        public IAsyncEnumerable<CardDto> ListByCardContainerAndTagsAsync(
            string parentId,
            IReadOnlyList<string> tagIds,
            TagMatchMode matchMode,
            int skip,
            int take,
            CancellationToken ct = default)
            => _q.ListByCardContainerAndTagsAsync(parentId, tagIds, matchMode, skip, take, ct);

        public Task<IReadOnlyList<CardDto>> GetVariantsAsync(string parentCardId, CancellationToken ct = default)
            => _vq.GetVariantsAsync(parentCardId, ct);

        public Task<IReadOnlyList<CardDto>> GetGroupAsync(string anyCardId, CancellationToken ct = default)
            => _vq.GetGroupAsync(anyCardId, ct);
    }
}
