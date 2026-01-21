using System.Collections.Generic;
using System.Threading;

namespace Database.Abstractions.Queries
{
    public sealed record TrashItem(string Id, string Type, string Name, string? ParentId, long UpdatedAtUtc);

    public interface ITrashQueries
    {
        IAsyncEnumerable<TrashItem> ListAsync(string worldId, int skip, int take, CancellationToken ct = default);
    }
}