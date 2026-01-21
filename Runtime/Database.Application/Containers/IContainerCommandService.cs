using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Containers;
using BadWriter.Contracts.Enums;

namespace Database.Application.Containers {
public interface IContainerCommandService
{
    Task<ContainerDto> CreateAsync(
        string worldId,
        string name,
        int order,
        string? parentId,
        ContainerContentType contentType,
        string description,
        CancellationToken ct);
    Task<ContainerDto> RenameAsync(string id, string name, CancellationToken ct);
    Task<ContainerDto> SetDescriptionAsync(string id, string description, CancellationToken ct);
    Task<ContainerDto> ReorderAsync(string id, int order, CancellationToken ct);
    Task<ContainerDto> MoveAsync(string id, string? parentId, int order, CancellationToken ct);
    Task DeleteAsync(string id, CancellationToken ct);
    Task<bool> RestoreAsync(string id, CancellationToken ct = default);
    Task<int> PurgeAsync(string id, CancellationToken ct = default);
}
}
