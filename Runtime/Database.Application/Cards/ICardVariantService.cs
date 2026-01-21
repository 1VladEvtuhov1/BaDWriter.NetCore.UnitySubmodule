using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Cards;

namespace Database.Application.Cards
{
    public interface ICardVariantService
    {
        Task<IReadOnlyList<CardDto>> ListVariantsAsync(string rootCardId, CancellationToken ct);
        Task<CardDto> CreateVariantAsync(string rootCardId, CardDto payload, CancellationToken ct);
        Task DeleteVariantAsync(string variantCardId, CancellationToken ct);
        Task ReorderVariantsAsync(string rootCardId, IReadOnlyList<string> orderedVariantIds, CancellationToken ct);
    }
}