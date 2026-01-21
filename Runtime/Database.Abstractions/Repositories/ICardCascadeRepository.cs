using System.Threading;
using System.Threading.Tasks;

namespace Database.Abstractions.Repositories
{
    public interface ICardCascadeRepository
    {
        Task<int> PurgeAsync(string id, CancellationToken ct = default);
    }
}