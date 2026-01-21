using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Containers;

namespace Database.Abstractions.Queries
{
    public interface IContainerQueries
    {
        Task<ContainerDto> GetAsync(string id, CancellationToken ct = default);

        IAsyncEnumerable<ContainerDto> ListByWorldAsync(
            string worldId, int skip, int take, CancellationToken ct = default);

        IAsyncEnumerable<ContainerDto> ListByParentAsync(
            string parentId, int skip, int take, CancellationToken ct = default);

        IAsyncEnumerable<ContainerDto> GetUpdatedSinceAsync(
            long since, string? afterId, int take, CancellationToken ct = default);

        Task<bool> ExistsByNameAsync(
            string worldId, string? parentId, string name, string? excludeId = null,
            CancellationToken ct = default);
    }
}