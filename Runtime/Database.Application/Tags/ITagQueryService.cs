
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Tags;

namespace Database.Application.Tags
{
    public interface ITagQueryService
    {
        Task<TagDto> GetAsync(string id, CancellationToken ct);
        IAsyncEnumerable<TagDto> SearchByTextAsync(string? text, int skip, int take, CancellationToken ct);
        Task<IReadOnlyList<string>> GetMissingAsync(IEnumerable<string>? ids, CancellationToken ct);
    }
}