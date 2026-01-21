// Path: Database.Abstractions/Queries/ICardQueries.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Cards;
using Database.Application.Queries;

namespace Database.Abstractions.Queries
{
    public interface ICardQueries
    {
        IAsyncEnumerable<CardDto> ListByCardContainerAsync(
            string parentId,
            int skip,
            int take,
            CancellationToken ct = default);

        IAsyncEnumerable<CardDto> ListByCardContainerAndTagsAsync(
            string parentId,
            IReadOnlyList<string> tagIds,
            TagMatchMode matchMode,
            int skip,
            int take,
            CancellationToken ct = default);

        Task<CardDto?> GetAsync(string id, CancellationToken ct = default);

        IAsyncEnumerable<CardDto> GetUpdatedSinceAsync(
            long since,
            string? afterId,
            int take,
            CancellationToken ct = default);

        Task<bool> ExistsByNameAsync(
            string parentId,
            string name,
            string? excludeId = null,
            CancellationToken ct = default);
    }
}