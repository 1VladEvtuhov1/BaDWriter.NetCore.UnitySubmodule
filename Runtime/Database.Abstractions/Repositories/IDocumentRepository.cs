using System.Threading;
using System.Threading.Tasks;

namespace Database.Abstractions.Repositories
{
    public interface IDocumentRepository<T>
    {
        Task<T> GetAsync(string id, CancellationToken ct = default);
        Task UpsertAsync(T doc, long? expectedVersion = null, CancellationToken ct = default);
        Task<bool> DeleteAsync(string id, long? expectedVersion = null, CancellationToken ct = default);
    }
}