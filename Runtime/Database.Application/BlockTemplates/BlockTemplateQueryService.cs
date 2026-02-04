using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Blocks;
using Database.Abstractions.Repositories;

namespace Database.Abstractions.Queries
{
    public sealed class BlockTemplateQueryService : IBlockTemplateQueryService
    {
        private readonly IBlockTemplateQueries _q;
        private readonly IDocumentRepository<BlockTemplateDto> _repo;

        public BlockTemplateQueryService(IBlockTemplateQueries q, IDocumentRepository<BlockTemplateDto> repo)
        {
            _q = q ?? throw new ArgumentNullException(nameof(q));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        }

        public Task<BlockTemplateDto?> GetAsync(string id, CancellationToken ct) =>
            _repo.GetAsync(id, ct);

        public IAsyncEnumerable<BlockTemplateDto> ListAsync(int skip, int take, CancellationToken ct) =>
            _q.ListAsync(Math.Max(0, skip), Math.Max(1, take), ct);

        public async Task<bool> ExistsAsync(string id, CancellationToken ct) =>
            await _repo.GetAsync(id, ct) is not null;
    }
}