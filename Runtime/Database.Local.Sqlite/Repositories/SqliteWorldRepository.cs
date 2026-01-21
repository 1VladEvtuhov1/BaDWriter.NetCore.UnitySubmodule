using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Worlds;
using Database.Abstractions.Queries;
using Database.Abstractions.Repositories;
using Database.Local.Sqlite.Sqlite;
using Microsoft.Data.Sqlite;

namespace Database.Local.Sqlite.Repositories
{
    /// <summary>
    /// SQLite repository for Worlds (DTO-first, netstandard2.1).
    /// CRUD + каскады + change-feed.
    /// </summary>
    public sealed class SqliteWorldRepository :
        IDocumentRepository<WorldDto>,
        IWorldQueries,
        IWorldCascadeRepository
    {
        private readonly ISqliteConnectionFactory _factory;
        public SqliteWorldRepository(ISqliteConnectionFactory factory)
            => _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        
        public async Task<bool> ExistsByNameAsync(string name, string? excludeId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            const string sql = @"
SELECT 1
  FROM worlds
 WHERE is_deleted = 0
   AND name = @name COLLATE NOCASE
   AND (@excludeId IS NULL OR id <> @excludeId)
 LIMIT 1;";

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@name", name.Trim());
            cmd.Parameters.AddWithValue("@excludeId", (object?)excludeId ?? DBNull.Value);

            var x = await cmd.ExecuteScalarAsync(ct);
            return x is not null;
        }


        // ---------------- IDocumentRepository<WorldDto> ----------------

        public async Task<WorldDto?> GetAsync(string id, CancellationToken ct = default)
        {
            const string sql = @"
SELECT id, name, description, version, updated_at_utc, is_deleted
FROM worlds
WHERE id = @id AND is_deleted = 0
LIMIT 1;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;

            return Map(r);
        }

        public async Task UpsertAsync(WorldDto doc, long? expectedVersion = null, CancellationToken ct = default)
        {
            if (doc is null) throw new ArgumentNullException(nameof(doc));
            var id = string.IsNullOrWhiteSpace(doc.Id) ? Guid.NewGuid().ToString("N") : doc.Id;

            const string sql = @"
INSERT INTO worlds (id, name, description, version, updated_at_utc, is_deleted)
VALUES (@id, @name, @desc, COALESCE(@ver, 0), strftime('%s','now'), 0)
ON CONFLICT(id) DO UPDATE SET
    name            = excluded.name,
    description     = excluded.description,
    version         = worlds.version + 1,
    updated_at_utc  = excluded.updated_at_utc,
    is_deleted      = 0;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using (var cmd = new SqliteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@name", doc.Name ?? string.Empty);
                cmd.Parameters.AddWithValue("@desc", doc.Description ?? string.Empty);
                cmd.Parameters.AddWithValue("@ver", (object?)expectedVersion ?? DBNull.Value);

                var rows = await cmd.ExecuteNonQueryAsync(ct);
                if (rows <= 0) throw new InvalidOperationException("Upsert(world) failed.");
            }
        }

        public async Task<bool> DeleteAsync(string id, long? expectedVersion = null, CancellationToken ct = default)
        {
            const string sql = @"
UPDATE worlds
SET is_deleted = 1,
    updated_at_utc = strftime('%s','now'),
    version = version + 1
WHERE id = @id AND is_deleted = 0;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);

            var affected = await cmd.ExecuteNonQueryAsync(ct);
            return affected > 0;
        }

        // ---------------- IWorldCascadeRepository ----------------

        public async Task<bool> SoftDeleteCascadeAsync(string worldId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(worldId))
                throw new ArgumentException("worldId is required", nameof(worldId));

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

            int rowsCards, rowsContainers, rowsWorld;

            // cards
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
UPDATE cards
SET is_deleted = 1,
    updated_at_utc = strftime('%s','now'),
    version = version + 1
WHERE is_deleted = 0
  AND parent_id IN (SELECT id FROM containers WHERE world_id=@wid AND is_deleted=0);";
                cmd.Parameters.AddWithValue("@wid", worldId);
                rowsCards = await cmd.ExecuteNonQueryAsync(ct);
            }

            // containers
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
UPDATE containers
SET is_deleted = 1,
    updated_at_utc = strftime('%s','now'),
    version = version + 1
WHERE world_id=@wid AND is_deleted=0;";
                cmd.Parameters.AddWithValue("@wid", worldId);
                rowsContainers = await cmd.ExecuteNonQueryAsync(ct);
            }

            // world
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
UPDATE worlds
SET is_deleted = 1,
    updated_at_utc = strftime('%s','now'),
    version = version + 1
WHERE id=@wid AND is_deleted=0;";
                cmd.Parameters.AddWithValue("@wid", worldId);
                rowsWorld = await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return (rowsWorld + rowsContainers + rowsCards) > 0;
        }

        public async Task<bool> RestoreCascadeAsync(string worldId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(worldId))
                throw new ArgumentException("worldId is required", nameof(worldId));

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

            int rowsWorld, rowsContainers, rowsCards;

            // world
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
UPDATE worlds
SET is_deleted = 0,
    updated_at_utc = strftime('%s','now'),
    version = version + 1
WHERE id=@wid;";
                cmd.Parameters.AddWithValue("@wid", worldId);
                rowsWorld = await cmd.ExecuteNonQueryAsync(ct);
            }

            // containers
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
UPDATE containers
SET is_deleted = 0,
    updated_at_utc = strftime('%s','now'),
    version = version + 1
WHERE world_id=@wid;";
                cmd.Parameters.AddWithValue("@wid", worldId);
                rowsContainers = await cmd.ExecuteNonQueryAsync(ct);
            }

            // cards
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
UPDATE cards
SET is_deleted = 0,
    updated_at_utc = strftime('%s','now'),
    version = version + 1
WHERE parent_id IN (SELECT id FROM containers WHERE world_id=@wid);";
                cmd.Parameters.AddWithValue("@wid", worldId);
                rowsCards = await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return (rowsWorld + rowsContainers + rowsCards) > 0;
        }

        public async Task<int> PurgeCascadeAsync(string worldId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(worldId))
                throw new ArgumentException("worldId is required", nameof(worldId));

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

            int total = 0;

            // card_tags
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
DELETE FROM card_tags
WHERE card_id IN (
  SELECT k.id FROM cards k
  JOIN containers c ON c.id = k.parent_id
  WHERE c.world_id = @wid
);";
                cmd.Parameters.AddWithValue("@wid", worldId);
                total += await cmd.ExecuteNonQueryAsync(ct);
            }

            // cards
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
DELETE FROM cards
WHERE parent_id IN (SELECT id FROM containers WHERE world_id=@wid);";
                cmd.Parameters.AddWithValue("@wid", worldId);
                total += await cmd.ExecuteNonQueryAsync(ct);
            }

            // containers
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"DELETE FROM containers WHERE world_id=@wid;";
                cmd.Parameters.AddWithValue("@wid", worldId);
                total += await cmd.ExecuteNonQueryAsync(ct);
            }

            // world
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"DELETE FROM worlds WHERE id=@wid;";
                cmd.Parameters.AddWithValue("@wid", worldId);
                total += await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return total;
        }

        // ---------------- IWorldQueries (DTO) ----------------

        public async IAsyncEnumerable<WorldDto> SearchByTextAsync(
            string text, int skip, int take,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (skip < 0) skip = 0;
            if (take <= 0) take = 100;
            if (take > 500) take = 500;

            text ??= string.Empty;
            var like = "%" + text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";

            const string sql = @"
SELECT id, name, description, version, updated_at_utc, is_deleted
FROM worlds
WHERE is_deleted = 0
  AND (name LIKE @like ESCAPE '\' OR description LIKE @like ESCAPE '\')
ORDER BY updated_at_utc DESC, id
LIMIT @take OFFSET @skip;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@like", like);
            cmd.Parameters.AddWithValue("@take", take);
            cmd.Parameters.AddWithValue("@skip", skip);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                yield return Map(r);
        }

        /// <summary>
        /// Change feed (cursor-based).
        /// </summary>
        public async IAsyncEnumerable<WorldDto> GetUpdatedSinceAsync(
            long since, string? afterId, int take,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            afterId ??= string.Empty;

            const string sql = @"
SELECT id, name, description, version, updated_at_utc, is_deleted
FROM worlds
WHERE (updated_at_utc > @since)
   OR (updated_at_utc = @since AND id > @after)
ORDER BY updated_at_utc ASC, id ASC
LIMIT @take;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@since", since);
            cmd.Parameters.AddWithValue("@after", afterId);
            cmd.Parameters.AddWithValue("@take", take);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                yield return Map(r);
        }

        public async IAsyncEnumerable<WorldDto> ListAllAsync(
            int skip = 0,
            int take = 100,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (skip < 0) skip = 0;
            if (take <= 0) take = 100;
            if (take > 500) take = 500;

            const string sql = @"
SELECT id, name, description, version, updated_at_utc, is_deleted
FROM worlds
WHERE is_deleted = 0
ORDER BY updated_at_utc DESC, id
LIMIT @take OFFSET @skip;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@take", take);
            cmd.Parameters.AddWithValue("@skip", skip);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                ct.ThrowIfCancellationRequested();
                yield return Map(r);
            }
        }

        private static WorldDto Map(SqliteDataReader r)
        {
            return new WorldDto(
                Id: r.GetString(0),
                Name: r.GetString(1),
                Description: r.GetString(2),
                Version: r.GetInt32(3),
                UpdatedAtUtc: r.GetInt64(4),
                IsDeleted: r.GetBoolean(5)
            );
        }

        // ---------------- helpers ----------------

        private static WorldDto Map(IDataRecord r)
        {
            var id        = r.GetString(0);
            var name      = r.GetString(1);
            var desc      = r.IsDBNull(2) ? string.Empty : r.GetString(2);
            var version   = r.GetInt64(3);
            var updated   = r.GetInt64(4);
            var isDeleted = r.GetBoolean(5);

            return new WorldDto(
                Id: id,
                Name: name,
                Description: desc,
                Version: version,
                UpdatedAtUtc: updated,
                IsDeleted: isDeleted
            );
        }
    }
}
