
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Containers;

namespace Database.Application.Containers {

public interface IContainerQueryService
{
    Task<ContainerDto> GetAsync(string id, CancellationToken ct);
    IAsyncEnumerable<ContainerDto> ListByWorldAsync(string worldId, int skip, int take, CancellationToken ct);
    IAsyncEnumerable<ContainerDto> ListByParentAsync(string parentId, int skip, int take, CancellationToken ct = default);
    Task<bool> ExistsAsync(string id, CancellationToken ct);
}
}
