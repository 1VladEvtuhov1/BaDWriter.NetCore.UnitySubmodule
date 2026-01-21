using System.Threading;
using System.Threading.Tasks;

namespace Database.Abstractions.Repositories
{
    public interface IWorldCascadeRepository
    {
        Task<bool>  SoftDeleteCascadeAsync(string worldId, CancellationToken ct = default);
        Task<bool>  RestoreCascadeAsync(string worldId,    CancellationToken ct = default);
        Task<int>   PurgeCascadeAsync(string worldId,      CancellationToken ct = default);
    }
}