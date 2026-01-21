using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Containers;
using BadWriter.Contracts.Enums;
using BadWriter.Contracts.Worlds;
using Database.Abstractions.Queries;
using Database.Abstractions.Repositories;
using UnityEngine;

namespace Database.Application.Containers
{
    public sealed class ContainerCommandService : IContainerCommandService
    {
        private const int MaxNameLen = 200;
        private const int MaxDescLen = 100_000;

        private readonly IDocumentRepository<ContainerDto> _repo;
        private readonly IDocumentRepository<WorldDto> _worlds;
        private readonly IContainerQueries _queries;
        private readonly IContainerCascadeRepository _cascade;

        public ContainerCommandService(
            IDocumentRepository<ContainerDto> repo,
            IDocumentRepository<WorldDto> worlds,
            IContainerQueries queries,
            IContainerCascadeRepository cascade)
        {
            _repo     = repo     ?? throw new ArgumentNullException(nameof(repo));
            _worlds   = worlds   ?? throw new ArgumentNullException(nameof(worlds));
            _queries  = queries  ?? throw new ArgumentNullException(nameof(queries));
            _cascade  = cascade  ?? throw new ArgumentNullException(nameof(cascade));
        }

        public async Task<ContainerDto> CreateAsync(
            string worldId,
            string name,
            int order,
            string? parentId,
            ContainerContentType contentType,
            string description,
            CancellationToken ct)
        {
            Debug.Log($"[ContainerCommands] Creating container with type: {contentType}");
            if (order < 0)
                throw new ArgumentOutOfRangeException(nameof(order));

            var normalizedName = NormalizeName(name);
            if (normalizedName.Length == 0)
                throw new ArgumentException("name is required", nameof(name));
            if (normalizedName.Length > MaxNameLen)
                throw new ArgumentOutOfRangeException(nameof(name));

            description ??= string.Empty;
            if (description.Length > MaxDescLen)
                throw new ArgumentOutOfRangeException(nameof(description));

            string? parent = string.IsNullOrWhiteSpace(parentId) ? null : parentId.Trim();
            string effectiveWorldId;

            if (parent is not null)
            {
                var parentDto = await _queries.GetAsync(parent, ct)
                               ?? throw new KeyNotFoundException($"parent container '{parent}' not found");

                if (string.IsNullOrWhiteSpace(parentDto.WorldId))
                    throw new InvalidOperationException("Parent container has no WorldId.");

                if (!string.IsNullOrWhiteSpace(worldId) &&
                    !string.Equals(worldId, parentDto.WorldId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Cannot create a child container in a different world than its parent.");

                effectiveWorldId = parentDto.WorldId!;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(worldId))
                    throw new ArgumentException("worldId is required for root container", nameof(worldId));

                if (await _worlds.GetAsync(worldId, ct) is null)
                    throw new KeyNotFoundException($"world '{worldId}' not found");

                effectiveWorldId = worldId;
            }

            if (await _queries.ExistsByNameAsync(effectiveWorldId, parent, normalizedName, excludeId: null, ct))
                throw new DuplicateNameException("Container " + "Pid = " + (parent ?? string.Empty) + "Name = " + normalizedName);

            var dto = new ContainerDto(
                Id:            Guid.NewGuid().ToString("N"),
                Name:          normalizedName,
                Description:   description,
                WorldId:       effectiveWorldId,
                ParentId:      parent,
                Order:         order,
                ContentType:   contentType,
                Version:       0,
                UpdatedAtUtc:  0,
                IsDeleted:     false
            );

            await _repo.UpsertAsync(dto, expectedVersion: null, ct);
            return await _repo.GetAsync(dto.Id, ct) ?? dto;
        }

        public async Task<ContainerDto> RenameAsync(string id, string name, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("id is required", nameof(id));

            var current = await _repo.GetAsync(id, ct)
                          ?? throw new KeyNotFoundException($"container '{id}' not found");

            var normalizedName = NormalizeName(name);
            if (normalizedName.Length == 0)
                throw new ArgumentException("name is required", nameof(name));
            if (normalizedName.Length > MaxNameLen)
                throw new ArgumentOutOfRangeException(nameof(name));

            if (string.Equals(current.Name, normalizedName, StringComparison.Ordinal))
                return current;

            if (await _queries.ExistsByNameAsync(current.WorldId!, current.ParentId, normalizedName, excludeId: current.Id, ct))
                throw new DuplicateNameException("Container " + "Pid = " + (current.ParentId ?? string.Empty) + "Name = " + normalizedName);

            var updated = current with { Name = normalizedName };
            await _repo.UpsertAsync(updated, expectedVersion: current.Version, ct);
            return await _repo.GetAsync(id, ct) ?? updated;
        }

        public async Task<ContainerDto> SetDescriptionAsync(string id, string description, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("id is required", nameof(id));

            description ??= string.Empty;
            if (description.Length > MaxDescLen)
                throw new ArgumentOutOfRangeException(nameof(description));

            var current = await _repo.GetAsync(id, ct)
                          ?? throw new KeyNotFoundException($"container '{id}' not found");

            var currentDesc = current.Description ?? string.Empty;
            if (string.Equals(currentDesc, description, StringComparison.Ordinal))
                return current;

            var updated = current with { Description = description };
            await _repo.UpsertAsync(updated, expectedVersion: current.Version, ct);

            return await _repo.GetAsync(id, ct) ?? updated;
        }

        
        public async Task<bool> RestoreAsync(string id, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Id is required.", nameof(id));

            return await _cascade.RestoreCascadeAsync(id, ct);
        }

        public Task<int> PurgeAsync(string id, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Id is required.", nameof(id));

            return _cascade.PurgeCascadeAsync(id, ct);
        }

        public async Task<ContainerDto> ReorderAsync(string id, int order, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("id is required", nameof(id));
            if (order < 0)
                throw new ArgumentOutOfRangeException(nameof(order));

            var current = await _repo.GetAsync(id, ct)
                          ?? throw new KeyNotFoundException($"container '{id}' not found");

            if (current.Order == order) return current;

            var updated = current with { Order = order };
            await _repo.UpsertAsync(updated, expectedVersion: current.Version, ct);
            return await _repo.GetAsync(id, ct) ?? updated;
        }

        public async Task<ContainerDto> MoveAsync(string id, string? parentId, int order, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("id is required", nameof(id));
            if (order < 0)
                throw new ArgumentOutOfRangeException(nameof(order));

            var current = await _repo.GetAsync(id, ct)
                          ?? throw new KeyNotFoundException($"container '{id}' not found");

            var newParent = string.IsNullOrWhiteSpace(parentId) ? null : parentId.Trim();
            var parentChanged = !string.Equals(current.ParentId ?? string.Empty, newParent ?? string.Empty, StringComparison.Ordinal);

            if (parentChanged && newParent is not null)
            {
                var parentDto = await _queries.GetAsync(newParent, ct)
                               ?? throw new KeyNotFoundException($"parent container '{newParent}' not found");

                if (!string.Equals(current.WorldId, parentDto.WorldId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Cannot move container across worlds.");

                if (await _queries.ExistsByNameAsync(current.WorldId!, newParent, current.Name, excludeId: current.Id, ct))
                    throw new DuplicateNameException("Container " + "Pid = " + newParent + "Name = " + current.Name);
            }

            if (parentChanged && newParent is null)
            {
                if (await _queries.ExistsByNameAsync(current.WorldId!, null, current.Name, excludeId: current.Id, ct))
                    throw new DuplicateNameException("Container " + "Pid = (root)" + "Name = " + current.Name);
            }

            var updated = current with { ParentId = newParent, Order = order };
            await _repo.UpsertAsync(updated, expectedVersion: current.Version, ct);
            return await _repo.GetAsync(id, ct) ?? updated;
        }

        public async Task DeleteAsync(string id, CancellationToken ct)
        {
            var current = await _repo.GetAsync(id, ct);
            await _repo.DeleteAsync(id, expectedVersion: current?.Version, ct);
        }

        private static readonly Regex Ws = new(@"[\s]+", RegexOptions.Compiled);

        private static string NormalizeName(string input)
        {
            var trimmed = (input ?? string.Empty).Trim();
            return Ws.Replace(trimmed, " ");
        }
    }
}
