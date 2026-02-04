using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Blocks;
using BadWriter.Contracts.Content;
using Database.Abstractions.Queries;
using Database.Abstractions.Repositories;

namespace Database.Application.BlockTemplates
{
    public sealed class BlockTemplateCommandService : IBlockTemplateCommandService
    {
        private const int MaxIdLen = 64;
        private const int MaxNameLen = 200;

        private static readonly Regex Ws = new(@"[\s]+", RegexOptions.Compiled);
        private static readonly Regex IdRx = new(@"^[a-zA-Z0-9_\-]+$", RegexOptions.Compiled);

        private readonly IDocumentRepository<BlockTemplateDto> _repo;
        private readonly IBlockTemplateQueries _q;

        public BlockTemplateCommandService(
            IDocumentRepository<BlockTemplateDto> repo,
            IBlockTemplateQueries q)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _q = q ?? throw new ArgumentNullException(nameof(q));
        }

        public async Task<BlockTemplateDto> CreateAsync(string? id, string name, BlockDto payload, CancellationToken ct)
        {
            var tid = NormalizeId(id);
            var normalizedName = NormalizeName(name);

            if (normalizedName.Length == 0) throw new ArgumentException("name is required", nameof(name));
            if (normalizedName.Length > MaxNameLen) throw new ArgumentOutOfRangeException(nameof(name));

            payload = NormalizePayload(payload, templateId: tid);

            if (await _q.ExistsByNameAsync(normalizedName, excludeId: null, ct))
                throw new DuplicateNameException("BlockTemplate name already exists: " + normalizedName);

            var existingAny = await _q.GetAnyAsync(tid, ct);
            if (existingAny is not null && existingAny.IsDeleted == false)
                throw new InvalidOperationException("BlockTemplate id already exists: " + tid);

            var dto = new BlockTemplateDto(
                Id: tid,
                Name: normalizedName,
                Payload: payload,
                Version: 0,
                UpdatedAtUtc: 0,
                IsDeleted: false
            );

            await _repo.UpsertAsync(dto, expectedVersion: null, ct);
            return await _q.GetAnyAsync(dto.Id, ct) ?? dto;
        }

        public async Task<BlockTemplateDto> RenameAsync(string id, string name, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("id is required", nameof(id));

            var current = await _q.GetAnyAsync(id.Trim(), ct)
                          ?? throw new KeyNotFoundException("BlockTemplate not found: " + id);

            var normalizedName = NormalizeName(name);
            if (normalizedName.Length == 0) throw new ArgumentException("name is required", nameof(name));
            if (normalizedName.Length > MaxNameLen) throw new ArgumentOutOfRangeException(nameof(name));

            if (string.Equals(current.Name, normalizedName, StringComparison.Ordinal))
                return current;

            if (await _q.ExistsByNameAsync(normalizedName, excludeId: current.Id, ct))
                throw new DuplicateNameException("BlockTemplate name already exists: " + normalizedName);

            var updated = current with { Name = normalizedName };
            await _repo.UpsertAsync(updated, expectedVersion: current.Version, ct);
            return await _q.GetAnyAsync(updated.Id, ct) ?? updated;
        }

        public async Task<BlockTemplateDto> SetPayloadAsync(string id, BlockDto payload, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("id is required", nameof(id));

            var current = await _q.GetAnyAsync(id.Trim(), ct)
                          ?? throw new KeyNotFoundException("BlockTemplate not found: " + id);

            payload = NormalizePayload(payload, templateId: current.Id);

            var updated = current with { Payload = payload };
            await _repo.UpsertAsync(updated, expectedVersion: current.Version, ct);
            return await _q.GetAnyAsync(updated.Id, ct) ?? updated;
        }

        public async Task DeleteAsync(string id, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("id is required", nameof(id));

            var current = await _q.GetAnyAsync(id.Trim(), ct);
            await _repo.DeleteAsync(id.Trim(), expectedVersion: current?.Version, ct);
        }

        public async Task<bool> RestoreAsync(string id, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("id is required", nameof(id));

            var any = await _q.GetAnyAsync(id.Trim(), ct);
            if (any is null) return false;
            if (!any.IsDeleted) return true;

            var restored = any with { IsDeleted = false };
            await _repo.UpsertAsync(restored, expectedVersion: any.Version, ct);
            return true;
        }

        public async Task<int> PurgeAsync(string id, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("id is required", nameof(id));

            var any = await _q.GetAnyAsync(id.Trim(), ct);
            if (any is null) return 0;

            var deleted = await _repo.DeleteAsync(id.Trim(), expectedVersion: any.Version, ct);
            if (!deleted) return 0;

            return 1;
        }

        private static string NormalizeName(string input)
        {
            var trimmed = (input ?? string.Empty).Trim();
            return Ws.Replace(trimmed, " ");
        }

        private static string NormalizeId(string? id)
        {
            var trimmed = (id ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                trimmed = Guid.NewGuid().ToString("N");

            if (trimmed.Length > MaxIdLen)
                throw new ArgumentOutOfRangeException(nameof(id), "id is too long");

            if (!IdRx.IsMatch(trimmed))
                throw new ArgumentException("id must match [a-zA-Z0-9_-]+", nameof(id));

            return trimmed;
        }

        private static BlockDto NormalizePayload(BlockDto payload, string templateId)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            var children = payload.Children ?? new List<ElementDto>();

            for (int i = 0; i < children.Count; i++)
            {
                var e = children[i];
                if (e == null) throw new ArgumentException("Element is null", nameof(payload));
                if (string.IsNullOrWhiteSpace(e.Id))
                    children[i] = new ElementDto
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Type = e.Type,
                        Frame = e.Frame,
                        Anchors = e.Anchors,
                        Adaptation = e.Adaptation,
                        AspectRatio = e.AspectRatio,
                        Overflow = e.Overflow,
                        ZIndex = e.ZIndex,
                        Props = e.Props
                    };
            }

            return new BlockDto
            {
                Id = templateId,
                Children = children,
                DesignAspectRatio = payload.DesignAspectRatio,
                PaddingPx = payload.PaddingPx
            };
        }
    }
}
