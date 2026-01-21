using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Cards;

namespace Database.Abstractions.Repositories
{
    public interface ICardVariantRepository
    {
        Task<CardDto> CreateVariantAsync(string parentCardId, CardDto variant, CancellationToken ct);
        Task ReorderVariantsAsync(string parentCardId, IReadOnlyList<(string CardId, int Order)> newOrder, CancellationToken ct);

        // ✅ Добавляем это:
        Task DeleteVariantAsync(string variantCardId, CancellationToken ct);
    }
}