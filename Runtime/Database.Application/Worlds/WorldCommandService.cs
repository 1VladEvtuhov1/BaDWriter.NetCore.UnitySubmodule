using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Worlds;
using Database.Abstractions.Queries;
using Database.Abstractions.Repositories;

namespace Database.Application.Worlds
{
    public sealed class WorldCommandService : IWorldCommandService
    {
        private const int MaxNameLen = 200;
        private const int MaxDescLen = 100_000;

        private readonly IDocumentRepository<WorldDto> _repo;
        private readonly IWorldCascadeRepository _cascade;
        private readonly IWorldQueries _queries;

        public WorldCommandService(
            IDocumentRepository<WorldDto> repo,
            IWorldCascadeRepository cascade,
            IWorldQueries queries)
        {
            _repo    = repo    ?? throw new ArgumentNullException(nameof(repo));
            _cascade = cascade ?? throw new ArgumentNullException(nameof(cascade));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        }

        public async Task<WorldDto> CreateAsync(string name, string description, CancellationToken ct = default)
        {
            name = (name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("name is required", nameof(name));
            if (name.Length > MaxNameLen)
                throw new ArgumentOutOfRangeException(nameof(name));

            description = description ?? string.Empty;
            if (description.Length > MaxDescLen)
                throw new ArgumentOutOfRangeException(nameof(description));

            if (await _queries.ExistsByNameAsync(name, excludeId: null, ct))
                throw new DuplicateNameException($"World name '{name}' already exists.");

            var dto = new WorldDto(Guid.NewGuid().ToString("N"), name, description, 0, 0, false);
            await _repo.UpsertAsync(dto, expectedVersion: null, ct);
            return await _repo.GetAsync(dto.Id, ct) ?? dto;
        }

        public async Task RenameAsync(string id, string name, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Id is required.", nameof(id));

            name = (name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("name is required", nameof(name));
            if (name.Length > MaxNameLen)
                throw new ArgumentOutOfRangeException(nameof(name));

            if (await _queries.ExistsByNameAsync(name, excludeId: id, ct))
                throw new DuplicateNameException($"World name '{name}' already exists.");

            var current = await _repo.GetAsync(id, ct) ?? throw new KeyNotFoundException($"world '{id}' not found");
            var updated = current with { Name = name };
            await _repo.UpsertAsync(updated, expectedVersion: current.Version, ct);
        }

        public async Task SetDescriptionAsync(string id, string description, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Id is required.", nameof(id));

            description = description ?? string.Empty;
            if (description.Length > MaxDescLen)
                throw new ArgumentOutOfRangeException(nameof(description));

            var current = await _repo.GetAsync(id, ct) ?? throw new KeyNotFoundException($"world '{id}' not found");
            if (string.Equals(current.Description, description, StringComparison.Ordinal)) return;

            var updated = current with { Description = description };
            await _repo.UpsertAsync(updated, expectedVersion: current.Version, ct);
        }

        public async Task DeleteAsync(string id, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Id is required.", nameof(id));

            _ = await _cascade.SoftDeleteCascadeAsync(id, ct);
        }

        public Task<bool> RestoreAsync(string id, CancellationToken ct = default) =>
            _cascade.RestoreCascadeAsync(id, ct);

        public Task<int> PurgeAsync(string id, CancellationToken ct = default) =>
            _cascade.PurgeCascadeAsync(id, ct);
    }
}
