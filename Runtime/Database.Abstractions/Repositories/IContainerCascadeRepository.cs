using System.Threading;
using System.Threading.Tasks;

namespace Database.Abstractions.Repositories
{
    public interface IContainerCascadeRepository
    {
        Task<bool> SoftDeleteCascadeAsync(string id, CancellationToken ct = default);

        Task<bool> RestoreCascadeAsync(string id, CancellationToken ct = default);

        Task<int> PurgeCascadeAsync(string id, CancellationToken ct = default);
    }
}

