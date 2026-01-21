
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Tags;

namespace Database.Abstractions.Queries
{
    public interface ITagQueries
    {
        Task<TagDto> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default);
        Task<bool> ExistsByNormalizedNameAsync(string normalizedName, CancellationToken ct = default);
        IAsyncEnumerable<TagDto> SearchByTextAsync(string text, int skip, int take, CancellationToken ct = default);
    }
}