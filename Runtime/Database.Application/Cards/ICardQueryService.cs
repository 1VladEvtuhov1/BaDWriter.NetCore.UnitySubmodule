// Path: Database.Application/Cards/ICardQueryService.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Cards;
using Database.Abstractions.Queries;
using Database.Application.Queries;

namespace Database.Application.Cards
{
    public interface ICardQueryService
    {
        Task<CardDto> GetAsync(string id, CancellationToken ct = default);

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

        Task<IReadOnlyList<CardDto>> GetVariantsAsync(string parentCardId, CancellationToken ct = default);
        Task<IReadOnlyList<CardDto>> GetGroupAsync(string anyCardId, CancellationToken ct = default);
    }
}