using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Cards;
using Database.Abstractions.Queries;
using Database.Abstractions.Repositories;

namespace Database.Application.Cards
{
    public sealed class CardVariantService : ICardVariantService
    {
        private readonly ICardVariantQueries _queries;
        private readonly ICardVariantRepository _repo;

        public CardVariantService(ICardVariantQueries queries, ICardVariantRepository repo)
        {
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        }

        /// <summary>
        /// Returns all variants that belong to a specific root card.
        /// </summary>
        public Task<IReadOnlyList<CardDto>> ListVariantsAsync(string rootCardId, CancellationToken ct = default) =>
            _queries.GetVariantsAsync(rootCardId, ct);

        /// <summary>
        /// Returns all cards that belong to the same variant group as this card.
        /// </summary>
        public Task<IReadOnlyList<CardDto>> GetGroupAsync(string anyCardId, CancellationToken ct = default) =>
            _queries.GetGroupAsync(anyCardId, ct);

        /// <summary>
        /// Creates a new variant of the specified root card.
        /// </summary>
        public Task<CardDto> CreateVariantAsync(string parentCardId, CardDto variant, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(parentCardId))
                throw new ArgumentException("Parent card id is required", nameof(parentCardId));

            var normalized = variant with
            {
                ArtPath = PathNormalizer.NormalizeArtPath(variant.ArtPath)
            };

            return _repo.CreateVariantAsync(parentCardId, normalized, ct);
        }

        /// <summary>
        /// Deletes the variant card completely (soft delete in DB).
        /// </summary>
        public Task DeleteVariantAsync(string variantCardId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(variantCardId))
                throw new ArgumentException("Variant card id is required", nameof(variantCardId));

            return _repo.DeleteVariantAsync(variantCardId, ct);
        }

        /// <summary>
        /// Updates ordering of variants for the root card.
        /// </summary>
        public Task ReorderVariantsAsync(string rootCardId, IReadOnlyList<string> orderedVariantIds, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rootCardId))
                throw new ArgumentException("Root card id is required", nameof(rootCardId));

            if (orderedVariantIds == null || orderedVariantIds.Count == 0)
                throw new ArgumentException("Variants order list cannot be empty", nameof(orderedVariantIds));

            var newOrder = new List<(string CardId, int Order)>();
            for (int i = 0; i < orderedVariantIds.Count; i++)
                newOrder.Add((orderedVariantIds[i], i));

            return _repo.ReorderVariantsAsync(rootCardId, newOrder, ct);
        }
    }
}
