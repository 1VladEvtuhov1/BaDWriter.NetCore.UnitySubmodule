using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Cards;
using Database.Abstractions.Queries;
using Database.Abstractions.Repositories;
using Database.Application.Containers;

namespace Database.Application.Cards
{
    public sealed class CardCommandService : ICardCommandService
    {
        private const int MaxNameLength = 200;
        private const int MaxDescriptionLength = 100_000;

        private readonly IDocumentRepository<CardDto> _cards;
        private readonly IContainerQueryService _containers;
        private readonly ICardQueries _queries;
        private readonly ICardTagsRepository _cardTags;
        private readonly ICardCascadeRepository _cascade;
        private readonly ICardArtRepository _art;
        private readonly ICardLayoutService _cardLayouts;

        public CardCommandService(
            IDocumentRepository<CardDto> cards,
            IContainerQueryService containers,
            ICardQueries queries,
            ICardTagsRepository cardTags,
            ICardCascadeRepository cascade,
            ICardArtRepository art,
            ICardLayoutService cardLayouts)
        {
            _cards = cards ?? throw new ArgumentNullException(nameof(cards));
            _containers = containers ?? throw new ArgumentNullException(nameof(containers));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _cardTags = cardTags ?? throw new ArgumentNullException(nameof(cardTags));
            _cascade = cascade ?? throw new ArgumentNullException(nameof(cascade));
            _art = art ?? throw new ArgumentNullException(nameof(art));
            _cardLayouts = cardLayouts ?? throw new ArgumentNullException(nameof(cardLayouts));
        }

        public async Task<CardDto> CreateAsync(string parentId, string name, string? description, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(parentId))
                throw new ArgumentException("parentId is required.", nameof(parentId));

            var parent = await _containers.GetAsync(parentId, ct)
                         ?? throw new KeyNotFoundException($"Container '{parentId}' not found.");

            var normalized = NormalizeName(name);
            if (normalized.Length is < 1 or > MaxNameLength)
                throw new ArgumentOutOfRangeException(nameof(name), $"Name length must be 1..{MaxNameLength}.");

            if (await _queries.ExistsByNameAsync(parent.Id, normalized, excludeId: null, ct))
                throw new DuplicateNameException("Card " + "Pid = " + parent.Id + "Name = " + normalized);

            var id = Guid.NewGuid().ToString("N");

            var toCreate = new CardDto(
                Id:           id,
                Name:         normalized,
                ParentId:     parent.Id,
                ArtPath:      null,
                Description:  description ?? string.Empty,
                TagIds:       Array.Empty<string>(),
                Version:      0,
                UpdatedAtUtc: 0,
                IsDeleted:    false
            );


            await _cards.UpsertAsync(toCreate, expectedVersion: null, ct);
            return await _cards.GetAsync(id, ct) ?? toCreate;
        }

        public async Task<CardDto> PatchNameAsync(string id, string value, CancellationToken ct = default)
        {
            var current = await _cards.GetAsync(id, ct) ?? throw new KeyNotFoundException($"Card '{id}' not found.");
            var normalized = NormalizeName(value);
            if (normalized.Length is < 1 or > MaxNameLength)
                throw new ArgumentOutOfRangeException(nameof(value), $"Name length must be 1..{MaxNameLength}.");

            if (string.Equals(current.Name, normalized, StringComparison.Ordinal))
                return current;

            if (await _queries.ExistsByNameAsync(current.ParentId, normalized, excludeId: current.Id, ct))
                throw new DuplicateNameException("Card " + "Pid = " + current.ParentId + "Name = " + normalized);

            var updated = current with { Name = normalized };
            await _cards.UpsertAsync(updated, expectedVersion: current.Version, ct);
            return await _cards.GetAsync(id, ct) ?? updated;
        }

        public async Task<CardDto> PatchDescriptionAsync(string id, string description, CancellationToken ct = default)
        {
            var current = await _cards.GetAsync(id, ct) ?? throw new KeyNotFoundException($"Card '{id}' not found.");
            var desc = (description ?? string.Empty);
            if (desc.Length > MaxDescriptionLength)
                throw new ArgumentOutOfRangeException(nameof(description), $"Description too long (>{MaxDescriptionLength}).");

            if (string.Equals(current.Description, desc, StringComparison.Ordinal))
                return current;

            var updated = current with { Description = desc };
            await _cards.UpsertAsync(updated, expectedVersion: current.Version, ct);
            return await _cards.GetAsync(id, ct) ?? updated;
        }

        public async Task PatchArtPathAsync(string id, string? artPath, CancellationToken ct = default)
        {
            var card = await _queries.GetAsync(id, ct) ?? throw new KeyNotFoundException($"Card '{id}' not found.");
            var normalized = NormalizeArtPath(artPath);
            if (string.Equals(card.ArtPath, normalized, StringComparison.Ordinal))
                return;

            await _art.UpdateArtPathAsync(id, normalized, ct);
        }

        public async Task<CardDto> MoveAsync(string id, string parentId, int order, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(parentId))
                throw new ArgumentException("parentId is required.", nameof(parentId));

            var current = await _cards.GetAsync(id, ct) ?? throw new KeyNotFoundException($"Card '{id}' not found.");
            var target  = await _containers.GetAsync(parentId, ct) ?? throw new KeyNotFoundException($"Container '{parentId}' not found.");

            var newOrder = Math.Max(0, order);

            if (!string.Equals(current.ParentId, target.Id, StringComparison.Ordinal))
            {
                if (await _queries.ExistsByNameAsync(target.Id, current.Name, excludeId: current.Id, ct))
                    throw new DuplicateNameException("Card " + "Pid = " + target.Id + "Name = " + current.Name);
            }

            var updated = current with { ParentId = target.Id, VariantOrder = newOrder };
            await _cards.UpsertAsync(updated, expectedVersion: current.Version, ct);
            return await _cards.GetAsync(id, ct) ?? updated;
        }

        public async Task<CardDto> ReorderAsync(string id, int order, CancellationToken ct = default)
        {
            var current = await _cards.GetAsync(id, ct) ?? throw new KeyNotFoundException($"Card '{id}' not found.");
            var newOrder = Math.Max(0, order);

            if (current.VariantOrder == newOrder)
                return current;

            var updated = current with { VariantOrder = newOrder };
            await _cards.UpsertAsync(updated, expectedVersion: current.Version, ct);
            return await _cards.GetAsync(id, ct) ?? updated;
        }

        public async Task<CardDto> ReplaceTagsAsync(string id, IReadOnlyList<string> tagIds, CancellationToken ct = default)
        {
            var current = await _cards.GetAsync(id, ct) ?? throw new KeyNotFoundException($"Card '{id}' not found.");

            var normalized = (tagIds ?? Array.Empty<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            await _cardTags.ReplaceAsync(current.Id, normalized, ct);
            return current with { TagIds = normalized };
        }

        public async Task DeleteAsync(string id, CancellationToken ct = default)
        {
            var current = await _cards.GetAsync(id, ct);
            if (current == null)
                return;

            await _cardLayouts.DeleteLayoutAsync(id, ct);
            await _cards.DeleteAsync(id, expectedVersion: current.Version, ct);
        }

        public Task<int> PurgeAsync(string id, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Id is required.", nameof(id));
            return _cascade.PurgeAsync(id, ct);
        }

        private static string NormalizeName(string input)
        {
            var trimmed = (input ?? string.Empty).Trim();
            return Regex.Replace(trimmed, @"\s+", " ");
        }

        private static string? NormalizeArtPath(string? path)
        {
            var p = (path ?? string.Empty).Trim();
            return p.Length == 0 ? null : p.Replace('\\', '/');
        }
    }
}

