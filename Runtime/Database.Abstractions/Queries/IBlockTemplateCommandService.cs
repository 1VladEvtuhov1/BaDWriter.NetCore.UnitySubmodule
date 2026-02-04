using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Blocks;
using BadWriter.Contracts.Content;

namespace Database.Abstractions.Queries
{
    public interface IBlockTemplateCommandService
    {
        Task<BlockTemplateDto> CreateAsync(string? id, string name, BlockDto payload, CancellationToken ct);
        Task<BlockTemplateDto> RenameAsync(string id, string name, CancellationToken ct);
        Task<BlockTemplateDto> SetPayloadAsync(string id, BlockDto payload, CancellationToken ct);
        Task DeleteAsync(string id, CancellationToken ct);
        Task<bool> RestoreAsync(string id, CancellationToken ct);
        Task<int> PurgeAsync(string id, CancellationToken ct);
    }
}