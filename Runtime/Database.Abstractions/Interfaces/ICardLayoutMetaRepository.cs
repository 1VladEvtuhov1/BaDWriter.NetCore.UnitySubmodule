using System.Threading;
using System.Threading.Tasks;

namespace Database.Abstractions.Interfaces
{
    public interface ICardLayoutMetaRepository
    {
        Task UpdateLayoutMetaAsync(string cardId, bool hasLayout, long? layoutVersion, long? layoutUpdatedAtUtc, CancellationToken ct);
    }
}

