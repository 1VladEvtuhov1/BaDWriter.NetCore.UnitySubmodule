
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Cards;

namespace Database.Application.Cards {

public interface ICardCommandService
{
    Task<CardDto> CreateAsync(string parentId, string name, string description, CancellationToken ct);
    Task DeleteAsync(string id, CancellationToken ct);

    Task<CardDto> PatchNameAsync(string id, string name, CancellationToken ct);
    Task<CardDto> PatchDescriptionAsync(string id, string description, CancellationToken ct);

    Task PatchArtPathAsync(string id, string? artPath, CancellationToken ct = default);

    Task<CardDto> MoveAsync(string id, string parentId, int order, CancellationToken ct);
    Task<CardDto> ReorderAsync(string id, int order, CancellationToken ct);

    Task<CardDto> ReplaceTagsAsync(string id, IReadOnlyList<string> tagIds, CancellationToken ct);
    Task<int> PurgeAsync(string id, CancellationToken ct = default);
}
}
