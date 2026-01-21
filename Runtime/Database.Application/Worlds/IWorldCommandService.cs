using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Worlds;

namespace Database.Application.Worlds {

public interface IWorldCommandService
{
    Task<WorldDto> CreateAsync(string name, string description, CancellationToken ct = default);
    Task RenameAsync(string id, string name, CancellationToken ct);
    Task SetDescriptionAsync(string id, string description, CancellationToken ct);
    Task DeleteAsync(string id, CancellationToken ct= default);
    Task<bool> RestoreAsync(string id, CancellationToken ct = default);
    Task<int> PurgeAsync(string id, CancellationToken ct = default);
}
}
