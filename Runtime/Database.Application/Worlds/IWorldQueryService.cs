using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Worlds;

namespace Database.Application.Worlds
{
    public interface IWorldQueryService
    {
        Task<WorldDto> GetAsync(string id, CancellationToken ct);
        IAsyncEnumerable<WorldDto> SearchByTextAsync(string text, int skip, int take, CancellationToken ct);
        IAsyncEnumerable<WorldDto> ListAllAsync(int skip = 0, int take = 100, CancellationToken ct = default);
    }
}