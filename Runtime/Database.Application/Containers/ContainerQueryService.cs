using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Containers;
using Database.Abstractions.Queries;
using Database.Abstractions.Repositories;

namespace Database.Application.Containers
{
    public sealed class ContainerQueryService : IContainerQueryService
    {
        private readonly IContainerQueries _q;
        private readonly IDocumentRepository<ContainerDto> _repo;

        public ContainerQueryService(IContainerQueries q, IDocumentRepository<ContainerDto> repo)
        {
            _q = q ?? throw new ArgumentNullException(nameof(q));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        }

        public Task<ContainerDto> GetAsync(string id, CancellationToken ct) =>
            _repo.GetAsync(id, ct);

        public IAsyncEnumerable<ContainerDto> ListByWorldAsync(string worldId, int skip, int take, CancellationToken ct) =>
            _q.ListByWorldAsync(worldId, Math.Max(0, skip), Math.Max(1, take), ct);

        public async Task<bool> ExistsAsync(string id, CancellationToken ct) =>
            await _repo.GetAsync(id, ct) is not null;
        
        public async IAsyncEnumerable<ContainerDto> ListByParentAsync(
            string parentId, int skip, int take, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var x in _q.ListByParentAsync(parentId, skip, take, ct))
                yield return x;
        }

    }
}