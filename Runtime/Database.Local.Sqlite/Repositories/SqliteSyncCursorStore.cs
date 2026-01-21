// Database.Local.Sqlite.Repositories/SqliteSyncCursorStore.cs

using System.Threading;
using System.Threading.Tasks;
using Database.Local.Sqlite.Sqlite;
using Microsoft.Data.Sqlite;

namespace Database.Local.Sqlite.Repositories
{
    public sealed class SqliteSyncCursorStore : ISyncCursorStore
    {
        private readonly ISqliteConnectionFactory _factory;
        public SqliteSyncCursorStore(ISqliteConnectionFactory factory) => _factory = factory;

        public async Task<long?> GetCursorAsync(string worldId, string entity, CancellationToken ct = default)
        {
            const string sql = "SELECT cursor FROM sync_cursors WHERE world_id=@w AND entity=@e;";
            await using var conn = _factory.Create(); await conn.OpenAsync(ct);
            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@w", worldId);
            cmd.Parameters.AddWithValue("@e", entity);
            var val = await cmd.ExecuteScalarAsync(ct);
            return (val is long l) ? l : (val is int i ? i : (long?)null);
        }

        public async Task SetCursorAsync(string worldId, string entity, long cursor, CancellationToken ct = default)
        {
            const string sql = @"
INSERT INTO sync_cursors(world_id,entity,cursor) VALUES(@w,@e,@c)
ON CONFLICT(world_id,entity) DO UPDATE SET cursor=excluded.cursor;";
            await using var conn = _factory.Create(); await conn.OpenAsync(ct);
            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@w", worldId);
            cmd.Parameters.AddWithValue("@e", entity);
            cmd.Parameters.AddWithValue("@c", cursor);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public interface ISyncCursorStore
    {
        Task<long?> GetCursorAsync(string worldId, string entity, CancellationToken ct = default);
        Task SetCursorAsync(string worldId, string entity, long cursor, CancellationToken ct = default);
    }
}