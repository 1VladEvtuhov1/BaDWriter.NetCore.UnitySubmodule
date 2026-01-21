
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Worlds;

namespace Database.Abstractions.Queries
{
    public interface IWorldQueries
    {
        IAsyncEnumerable<WorldDto> SearchByTextAsync(string text, int skip, int take, CancellationToken ct = default);
        IAsyncEnumerable<WorldDto> GetUpdatedSinceAsync(long since, string? afterId, int take, CancellationToken ct = default);
        Task<bool> ExistsByNameAsync(string name, string? excludeId, CancellationToken ct = default);
        IAsyncEnumerable<WorldDto> ListAllAsync(int skip = 0, int take = 100, CancellationToken ct = default);
    }
}