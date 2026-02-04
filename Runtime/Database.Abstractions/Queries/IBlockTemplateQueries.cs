using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Blocks;

namespace Database.Abstractions.Queries
{
    public interface IBlockTemplateQueries
    {
        IAsyncEnumerable<BlockTemplateDto> ListAsync(int skip, int take, CancellationToken ct = default);
        IAsyncEnumerable<BlockTemplateDto> GetUpdatedSinceAsync(long since, string? afterId, int take, CancellationToken ct = default);

        Task<bool> ExistsByNameAsync(string name, string? excludeId = null, CancellationToken ct = default);
        Task<BlockTemplateDto?> GetAnyAsync(string id, CancellationToken ct = default);
    }
}