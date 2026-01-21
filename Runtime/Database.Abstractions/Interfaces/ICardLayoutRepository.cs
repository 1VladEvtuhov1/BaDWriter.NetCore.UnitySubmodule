using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Content;

namespace Database.Abstractions.Interfaces {

public interface ICardLayoutRepository
{
    Task<CardLayoutDto> GetByCardIdAsync(string cardId, CancellationToken ct);
    Task UpsertAsync(CardLayoutDto layout, CancellationToken ct);
    Task SoftDeleteAsync(string cardId, long updatedAtUtc, CancellationToken ct);
    Task<IReadOnlyList<CardLayoutDto>> GetUpdatedSinceAsync(long sinceUtc, int limit, CancellationToken ct);
}
}
