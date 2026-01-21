using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Worlds;
using Database.Abstractions.Queries;
using Database.Abstractions.Repositories;

namespace Database.Application.Worlds
{
    public sealed class WorldQueryService : IWorldQueryService
    {
        private readonly IWorldQueries _q;
        private readonly IDocumentRepository<WorldDto> _repo;

        public WorldQueryService(IWorldQueries q, IDocumentRepository<WorldDto> repo)
        {
            _q = q ?? throw new ArgumentNullException(nameof(q));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        }

        public Task<WorldDto> GetAsync(string id, CancellationToken ct) =>
            _repo.GetAsync(id, ct);

        public IAsyncEnumerable<WorldDto> SearchByTextAsync(string text, int skip, int take, CancellationToken ct) =>
            _q.SearchByTextAsync(text ?? string.Empty, Math.Max(0, skip), Math.Max(1, take), ct);

        public IAsyncEnumerable<WorldDto> ListAllAsync(int skip = 0, int take = 100, CancellationToken ct = default) => _q.ListAllAsync(skip, take, ct);

    }
}