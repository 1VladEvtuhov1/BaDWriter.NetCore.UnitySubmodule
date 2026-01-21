
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Tags;

namespace Database.Application.Tags {
public interface ITagCommandService
{
    Task<TagDto> CreateAsync(string name, int colorArgb, CancellationToken ct);
    Task<TagDto> RenameAsync(string id, string name, CancellationToken ct);
    Task<TagDto> SetColorAsync(string id, int colorArgb, CancellationToken ct);
    Task DeleteAsync(string id, CancellationToken ct);
}
}
