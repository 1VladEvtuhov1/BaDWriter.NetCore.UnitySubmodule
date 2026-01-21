using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Tags;
using Database.Abstractions.Queries;
using Database.Abstractions.Repositories;

namespace Database.Application.Tags
{
    public sealed class TagQueryService : ITagQueryService
    {
        private readonly ITagQueries _q;
        private readonly IDocumentRepository<TagDto> _repo;

        public TagQueryService(ITagQueries q, IDocumentRepository<TagDto> repo)
        {
            _q   = q   ?? throw new ArgumentNullException(nameof(q));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        }

        public Task<TagDto> GetAsync(string id, CancellationToken ct) =>
            _repo.GetAsync(id, ct);

        public IAsyncEnumerable<TagDto> SearchByTextAsync(string? text, int skip, int take, CancellationToken ct) =>
            _q.SearchByTextAsync(text ?? string.Empty, Math.Max(0, skip), Math.Max(1, take), ct);

        public async Task<IReadOnlyList<string>> GetMissingAsync(IEnumerable<string>? ids, CancellationToken ct)
        {
            if (ids is null) return Array.Empty<string>();

            var missing = new List<string>();
            foreach (var raw in ids)
            {
                var id = (raw).Trim();
                if (id.Length == 0) continue;

                var tag = await _repo.GetAsync(id, ct);
                if (tag is null) missing.Add(id);
            }
            return missing;
        }
    }
}