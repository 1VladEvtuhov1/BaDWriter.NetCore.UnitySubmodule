using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Blocks;

namespace Database.Abstractions.Queries
{
    public interface IBlockTemplateQueryService
    {
        Task<BlockTemplateDto?> GetAsync(string id, CancellationToken ct);
        IAsyncEnumerable<BlockTemplateDto> ListAsync(int skip, int take, CancellationToken ct);
        Task<bool> ExistsAsync(string id, CancellationToken ct);
    }
}