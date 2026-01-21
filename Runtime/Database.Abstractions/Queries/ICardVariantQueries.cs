
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Cards;

namespace Database.Abstractions.Queries {

public interface ICardVariantQueries
{
    Task<IReadOnlyList<CardDto>> GetVariantsAsync(string parentCardId, CancellationToken ct = default);
    Task<IReadOnlyList<CardDto>> GetGroupAsync(string anyCardId, CancellationToken ct = default);
}
}
