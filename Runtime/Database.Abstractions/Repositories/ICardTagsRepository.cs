using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Database.Abstractions.Repositories
{
    public interface ICardTagsRepository
    {
        Task<IReadOnlyList<string>> GetTagIdsAsync(string cardId, CancellationToken ct = default);
        Task ReplaceAsync(string cardId, IReadOnlyCollection<string> tagIds, CancellationToken ct = default);
        Task<Dictionary<string, string[]>> GetTagIdsForCardsAsync(IReadOnlyList<string> cardIds, CancellationToken ct = default);

        Task RemoveTagEverywhereAsync(string tagId, CancellationToken ct = default);
    }
}

