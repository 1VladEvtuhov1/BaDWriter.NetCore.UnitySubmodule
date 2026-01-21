using System;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Content;
using Database.Abstractions.Interfaces;

namespace Database.Application.Cards
{
    public sealed class CardLayoutService : ICardLayoutService
    {
        private readonly ICardLayoutRepository _repo;
        private readonly ICardLayoutMetaRepository _meta;

        public CardLayoutService(ICardLayoutRepository repo, ICardLayoutMetaRepository meta)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _meta = meta ?? throw new ArgumentNullException(nameof(meta));
        }

        public Task<CardLayoutDto> GetLayoutAsync(string cardId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("Card ID is required", nameof(cardId));

            return _repo.GetByCardIdAsync(cardId, ct);
        }

        public async Task SetLayoutAsync(string cardId, CardLayoutDto layout, CancellationToken ct = default)
        {
            Validate(layout);

            await _repo.UpsertAsync(layout, ct);
            await _meta.UpdateLayoutMetaAsync(
                cardId,
                hasLayout: true,
                layoutVersion: layout.LayoutVersion,
                layoutUpdatedAtUtc: layout.UpdatedAtUtc,
                ct);
        }

        public async Task DeleteLayoutAsync(string cardId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("Card ID is required", nameof(cardId));

            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            await _repo.SoftDeleteAsync(cardId, ts, ct);
            await _meta.UpdateLayoutMetaAsync(
                cardId,
                hasLayout: false,
                layoutVersion: null,
                layoutUpdatedAtUtc: null,
                ct);
        }

        private static void Validate(CardLayoutDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CardId))
                throw new ArgumentException("CardId is required.", nameof(dto));

            if (dto.LayoutVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(dto.LayoutVersion));

            if (dto.UpdatedAtUtc <= 0)
                throw new ArgumentOutOfRangeException(nameof(dto.UpdatedAtUtc));

            foreach (var block in dto.Blocks)
            foreach (var element in block.Children)
            {
                if (element.Frame.W <= 0 || element.Frame.W > 1.0)
                    throw new ArgumentOutOfRangeException($"Element {element.Id} Frame.W");

                if (element.Frame.H < 0 || element.Frame.H > 1.0)
                    throw new ArgumentOutOfRangeException($"Element {element.Id} Frame.H");

                if (element.Frame.X < 0 || element.Frame.X > 1.0)
                    throw new ArgumentOutOfRangeException($"Element {element.Id} Frame.X");

                if (element.Frame.Y < 0 || element.Frame.Y > 1.0)
                    throw new ArgumentOutOfRangeException($"Element {element.Id} Frame.Y");

                if (element.AspectRatio is <= 0)
                    throw new ArgumentOutOfRangeException($"Element {element.Id} AspectRatio");
            }
        }
    }
}
