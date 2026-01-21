using System.Threading;
using System.Threading.Tasks;

namespace Database.Abstractions.Repositories
{
    public interface ICardArtRepository
    {
        Task UpdateArtPathAsync(string cardId, string? artPath, CancellationToken ct);
    }
}

