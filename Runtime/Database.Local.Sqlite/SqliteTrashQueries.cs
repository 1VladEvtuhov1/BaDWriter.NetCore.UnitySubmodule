using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Database.Abstractions.Queries;
using Database.Local.Sqlite.Sqlite;

namespace Database.Local.Sqlite
{
    public sealed class SqliteTrashQueries : ITrashQueries
    {
        private readonly ISqliteConnectionFactory _factory;
        public SqliteTrashQueries(ISqliteConnectionFactory factory) => _factory = factory;

        public async IAsyncEnumerable<TrashItem> ListAsync(
            string worldId, int skip, int take, [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (skip < 0) skip = 0;
            if (take <= 0) take = 100;
            if (take > 1000) take = 1000;

            const string sql = @"
SELECT id, 'container' AS type, name, parent_id, updated_at_utc
FROM containers
WHERE world_id=@wid AND is_deleted=1
UNION ALL
SELECT k.id, 'card' AS type, k.name, k.parent_id, k.updated_at_utc
FROM cards k
WHERE k.is_deleted=1
  AND k.parent_id IN (SELECT id FROM containers WHERE world_id=@wid)
ORDER BY updated_at_utc DESC
LIMIT @take OFFSET @skip;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@wid", worldId);
            cmd.Parameters.AddWithValue("@take", take);
            cmd.Parameters.AddWithValue("@skip", skip);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var id   = r.GetString(0);
                var type = r.GetString(1);          // "container" | "card"
                var name = r.GetString(2);
                var pid  = r.IsDBNull(3) ? null : r.GetString(3);
                var ts   = r.GetInt64(4);

                yield return new TrashItem(id, type, name, pid, ts);
            }
        }
    }
}
