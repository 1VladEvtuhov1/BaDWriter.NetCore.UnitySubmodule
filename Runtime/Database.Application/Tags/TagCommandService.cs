using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Tags;
using Database.Abstractions.Repositories;

namespace Database.Application.Tags {

public sealed class TagCommandService : ITagCommandService
{
    private const int MaxNameLen = 200;
    private readonly IDocumentRepository<TagDto> _repo;

    public TagCommandService(IDocumentRepository<TagDto> repo) => _repo = repo;

    public async Task<TagDto> CreateAsync(string name, int colorArgb, CancellationToken ct)
    {
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name is required", nameof(name));
        if (name.Length > MaxNameLen)        throw new ArgumentOutOfRangeException(nameof(name), $"name too long (>{MaxNameLen})");

        var id = Guid.NewGuid().ToString("N");
        var t  = new TagDto(id, name, colorArgb);

        await _repo.UpsertAsync(t, expectedVersion: null, ct);

        return await _repo.GetAsync(id, ct) ?? t;
    }

    public async Task<TagDto> RenameAsync(string id, string name, CancellationToken ct)
    {
        var current = await _repo.GetAsync(id, ct) ?? throw new KeyNotFoundException($"tag '{id}' not found");
        var updated = current with { Name = (name ?? string.Empty).Trim() };

        // передаём ожидаемую версию для concurrency
        await _repo.UpsertAsync(updated, expectedVersion: current.Version, ct);

        return await _repo.GetAsync(id, ct) ?? updated;
    }

    public async Task<TagDto> SetColorAsync(string id, int colorArgb, CancellationToken ct)
    {
        var current = await _repo.GetAsync(id, ct) ?? throw new KeyNotFoundException($"tag '{id}' not found");

        await _repo.UpsertAsync(current, expectedVersion: current.Version, ct);

        return await _repo.GetAsync(id, ct) ?? current;
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        var current = await _repo.GetAsync(id, ct);
        await _repo.DeleteAsync(id, expectedVersion: current?.Version, ct);
    }
}
}
