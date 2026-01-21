using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Content;

namespace Database.Application.Cards
{
    public interface ICardLayoutService
    {
        Task<CardLayoutDto> GetLayoutAsync(string cardId, CancellationToken ct);
        Task SetLayoutAsync(string cardId, CardLayoutDto layout, CancellationToken ct);
        Task DeleteLayoutAsync(string cardId, CancellationToken ct);
    }
}