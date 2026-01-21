using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Containers;
using BadWriter.Contracts.Enums;
using Database.Abstractions.Queries;
using Database.Abstractions.Repositories;
using Database.Local.Sqlite.Sqlite;
using Microsoft.Data.Sqlite;

namespace Database.Local.Sqlite.Repositories
{
    /// <summary>
    /// SQLite repository for containers (DTO-first). Compatible with netstandard2.1.
    /// </summary>
    public sealed class SqliteContainerRepository :
        IDocumentRepository<ContainerDto>,
        IContainerQueries,
        IContainerCascadeRepository
    {
        private const int DefaultTake = 100;
        private const int MaxTake     = 500;

        private readonly ISqliteConnectionFactory _factory;

        public SqliteContainerRepository(ISqliteConnectionFactory factory)
            => _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        // ------------------- Queries -------------------

        public async Task<ContainerDto?> GetAsync(string id, CancellationToken ct = default)
        {
            const string sql = @"
SELECT id, name, description, world_id, parent_id, ""order"", content_type, version, updated_at_utc, is_deleted
FROM containers
WHERE id = @id AND is_deleted = 0;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@id", id);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;

            return MapContainer(r);
        }

        public async IAsyncEnumerable<ContainerDto> ListByWorldAsync(
            string worldId, int skip, int take,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            const string sql = @"
SELECT id, name, description, world_id, parent_id, ""order"", content_type, version, updated_at_utc, is_deleted
FROM containers
WHERE world_id = @wid AND is_deleted = 0
ORDER BY ""order"", id
LIMIT @take OFFSET @skip;";

            await foreach (var c in Query(sql, p => p.AddWithValue("@wid", worldId), skip, take, ct))
                yield return c;
        }

        public async IAsyncEnumerable<ContainerDto> ListByParentAsync(
            string parentId, int skip, int take,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            const string sql = @"
SELECT id, name, description, world_id, parent_id, ""order"", content_type, version, updated_at_utc, is_deleted
FROM containers
WHERE parent_id = @pid AND is_deleted = 0
ORDER BY ""order"", id
LIMIT @take OFFSET @skip;";

            await foreach (var c in Query(sql, p => p.AddWithValue("@pid", parentId), skip, take, ct))
                yield return c;
        }

        public async IAsyncEnumerable<ContainerDto> GetUpdatedSinceAsync(
            long since, string? afterId, int take,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            afterId ??= string.Empty;

            const string sql = @"
SELECT id, name, description, world_id, parent_id, ""order"", content_type, version, updated_at_utc, is_deleted
FROM containers
WHERE (updated_at_utc > @since) OR (updated_at_utc = @since AND id > @after)
ORDER BY updated_at_utc, id
LIMIT @take;";

            await foreach (var c in Query(
                               sql,
                               p =>
                               {
                                   p.AddWithValue("@since", since);
                                   p.AddWithValue("@after", afterId);
                               },
                               skip: 0, take: take, ct))
                yield return c;
        }

        public async Task<bool> ExistsByNameAsync(
            string worldId, string? parentId, string name, string? excludeId = null,
            CancellationToken ct = default)
        {
            const string sql = @"
SELECT 1
FROM containers
WHERE world_id = @wid
  AND COALESCE(parent_id,'') = COALESCE(@pid,'')
  AND TRIM(LOWER(name)) = TRIM(LOWER(@name))
  AND is_deleted = 0
  AND (@ex IS NULL OR id <> @ex)
LIMIT 1;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@wid", worldId);
            cmd.Parameters.AddWithValue("@pid", (object?)parentId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@name", name ?? string.Empty);
            cmd.Parameters.AddWithValue("@ex", (object?)excludeId ?? DBNull.Value);

            var exists = await cmd.ExecuteScalarAsync(ct);
            return exists is not null;
        }

        private async IAsyncEnumerable<ContainerDto> Query(
            string sql,
            Action<SqliteParameterCollection> bind,
            int skip,
            int take,
            [EnumeratorCancellation] CancellationToken ct)
        {
            if (skip < 0) skip = 0;
            if (take <= 0) take = DefaultTake;
            if (take > MaxTake) take = MaxTake;

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            bind(cmd.Parameters);
            cmd.Parameters.AddWithValue("@take", take);
            cmd.Parameters.AddWithValue("@skip", skip);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                yield return MapContainer(r);
        }

        private static ContainerDto MapContainer(IDataRecord r)
        {
            var id          = r.GetString(0);
            var name        = r.GetString(1);
            var description = r.IsDBNull(2) ? string.Empty : r.GetString(2);
            var worldId     = r.IsDBNull(3) ? null : r.GetString(3);
            var parentId    = r.IsDBNull(4) ? null : r.GetString(4);
            var order       = r.GetInt32(5);
            var ctype       = (ContainerContentType)r.GetInt32(6);
            var version     = r.GetInt64(7);
            var updated     = r.GetInt64(8);
            var isDeleted   = r.GetBoolean(9);

            return new ContainerDto(
                Id: id,
                Name: name,
                Description: description,
                WorldId: worldId,
                ParentId: parentId,
                Order: order,
                ContentType: ctype,
                Version: version,
                UpdatedAtUtc: updated,
                IsDeleted: isDeleted
            );
        }


        // ------------------- Upsert -------------------

        public async Task UpsertAsync(ContainerDto doc, long? expectedVersion = null, CancellationToken ct = default)
        {
            if (doc is null) throw new ArgumentNullException(nameof(doc));
            if (string.IsNullOrWhiteSpace(doc.WorldId))
                throw new ArgumentException("worldId is required", nameof(doc.WorldId));
            if (string.IsNullOrWhiteSpace(doc.Name))
                throw new ArgumentException("name is required", nameof(doc.Name));

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            // Uniqueness: (worldId, parentId, lower(name)) among alive except same id
            const string checkSql = @"
SELECT 1
FROM containers
WHERE world_id = @wid
  AND COALESCE(parent_id,'') = COALESCE(@pid,'')
  AND TRIM(LOWER(name)) = TRIM(LOWER(@name))
  AND is_deleted = 0
  AND id <> @id
LIMIT 1;";

            await using (var chk = conn.CreateCommand())
            {
                chk.CommandText = checkSql;
                chk.Parameters.AddWithValue("@wid", (object?)doc.WorldId ?? DBNull.Value);
                chk.Parameters.AddWithValue("@pid", (object?)doc.ParentId ?? DBNull.Value);
                chk.Parameters.AddWithValue("@name", doc.Name ?? string.Empty);
                chk.Parameters.AddWithValue("@id", string.IsNullOrWhiteSpace(doc.Id) ? "" : doc.Id);

                if (await chk.ExecuteScalarAsync(ct) is not null)
                    throw new DuplicateNameException("Container conflict: parent=" +
                        (doc.ParentId ?? string.Empty) + " name=" + (doc.Name ?? string.Empty));
            }

            const string upsertSql = @"
INSERT INTO containers (id, name, description, world_id, parent_id, ""order"", content_type, version, updated_at_utc, is_deleted)
VALUES (@id, @name, @desc, @wid, @pid, @ord, @ctype, COALESCE(@ver,0), strftime('%s','now'), 0)
ON CONFLICT(id) DO UPDATE SET
    name            = excluded.name,
    description     = excluded.description,
    world_id        = excluded.world_id,
    parent_id       = excluded.parent_id,
    ""order""         = excluded.""order"",
    content_type    = excluded.content_type,
    version = CASE
                WHEN @ver IS NULL THEN containers.version + 1
                ELSE containers.version + 1
              END,
    updated_at_utc  = excluded.updated_at_utc,
    is_deleted      = 0;";

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = upsertSql;
            cmd.Parameters.AddWithValue("@id", string.IsNullOrWhiteSpace(doc.Id) ? Guid.NewGuid().ToString("N") : doc.Id);
            cmd.Parameters.AddWithValue("@name", doc.Name ?? string.Empty);
            cmd.Parameters.AddWithValue("@desc", doc.Description ?? string.Empty); 
            cmd.Parameters.AddWithValue("@wid", (object?)doc.WorldId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pid", (object?)doc.ParentId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ord", doc.Order);
            cmd.Parameters.AddWithValue("@ctype", (int)doc.ContentType);
            cmd.Parameters.AddWithValue("@ver", (object?)expectedVersion ?? DBNull.Value);

            var rows = await cmd.ExecuteNonQueryAsync(ct);
            if (rows <= 0) throw new InvalidOperationException("Upsert(container) failed.");
        }

        // ------------------- Soft-delete (cascade) -------------------

        public async Task<bool> DeleteAsync(string id, long? expectedVersion = null, CancellationToken ct = default)
        {
            // expectedVersion зарезервирован — при необходимости можно усилить WHERE по версии.
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Id is required.", nameof(id));

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            // quick probe
            const string probeSql = @"SELECT is_deleted FROM containers WHERE id=@id LIMIT 1;";
            await using (var probeCmd = conn.CreateCommand())
            {
                probeCmd.CommandText = probeSql;
                probeCmd.Parameters.AddWithValue("@id", id);
                var probe = await probeCmd.ExecuteScalarAsync(ct);
                if (probe is null) return false;
                if ((probe is long l && l != 0) || (probe is int i && i != 0))
                    return false;
            }

            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

            // собрать временные таблицы с поддеревом
            await BuildSubtreeTemps(conn, tx, id, ct);

            int rowsCards;
            await using (var softDelCards = conn.CreateCommand())
            {
                softDelCards.Transaction = tx;
                softDelCards.CommandText = @"
UPDATE cards
SET is_deleted = 1,
    updated_at_utc = strftime('%s','now'),
    version = version + 1
WHERE parent_id IN (SELECT id FROM _tmp_cont_ids);";
                rowsCards = await softDelCards.ExecuteNonQueryAsync(ct);
            }

            int rowsContainers;
            await using (var softDelContainers = conn.CreateCommand())
            {
                softDelContainers.Transaction = tx;
                softDelContainers.CommandText = @"
UPDATE containers
SET is_deleted = 1,
    updated_at_utc = strftime('%s','now'),
    version = version + 1
WHERE id IN (SELECT id FROM _tmp_cont_ids);";
                rowsContainers = await softDelContainers.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return (rowsContainers + rowsCards) > 0;
        }

        public Task<bool> SoftDeleteCascadeAsync(string id, CancellationToken ct = default)
            => DeleteAsync(id, expectedVersion: null, ct);

        // ------------------- Restore (cascade + conflicts) -------------------

        public async Task<bool> RestoreCascadeAsync(string id, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Id is required.", nameof(id));

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

            // существует вообще?
            const string probe = "SELECT 1 FROM containers WHERE id=@id LIMIT 1;";
            await using (var p = conn.CreateCommand())
            {
                p.Transaction = tx;
                p.CommandText = probe;
                p.Parameters.AddWithValue("@id", id);
                if (await p.ExecuteScalarAsync(ct) is null)
                {
                    await tx.RollbackAsync(ct);
                    return false;
                }
            }

            await BuildSubtreeTemps(conn, tx, id, ct);

            var contConflict = await GetContainerRestoreConflictAsync(conn, tx, ct);
            if (contConflict is not null)
            {
                await tx.RollbackAsync(ct);
                throw new DuplicateNameException("Container conflict on restore: parent=" +
                    (contConflict.Value.parentId ?? string.Empty) + " name=" +
                    (contConflict.Value.name ?? string.Empty));
            }

            var cardConflict = await GetCardRestoreConflictAsync(conn, tx, ct);
            if (cardConflict is not null)
            {
                await tx.RollbackAsync(ct);
                throw new DuplicateNameException("Card conflict on restore: parent=" +
                    (cardConflict.Value.parentId ?? string.Empty) + " name=" +
                    (cardConflict.Value.name ?? string.Empty));
            }

            int rowsCards, rowsContainers;

            await using (var restoreCards = conn.CreateCommand())
            {
                restoreCards.Transaction = tx;
                restoreCards.CommandText = @"
UPDATE cards
SET is_deleted = 0,
    updated_at_utc = strftime('%s','now'),
    version = version + 1
WHERE parent_id IN (SELECT id FROM _tmp_cont_ids);";
                rowsCards = await restoreCards.ExecuteNonQueryAsync(ct);
            }

            await using (var restoreContainers = conn.CreateCommand())
            {
                restoreContainers.Transaction = tx;
                restoreContainers.CommandText = @"
UPDATE containers
SET is_deleted = 0,
    updated_at_utc = strftime('%s','now'),
    version = version + 1
WHERE id IN (SELECT id FROM _tmp_cont_ids);";
                rowsContainers = await restoreContainers.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return (rowsCards + rowsContainers) > 0;
        }

        // ------------------- Purge (hard delete) -------------------

        public async Task<int> PurgeCascadeAsync(string id, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Id is required.", nameof(id));

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

            await BuildSubtreeTemps(conn, tx, id, ct);

            int total = 0;

            await using (var delTags = conn.CreateCommand())
            {
                delTags.Transaction = tx;
                delTags.CommandText = @"DELETE FROM card_tags WHERE card_id IN (SELECT id FROM _tmp_card_ids);";
                total += await delTags.ExecuteNonQueryAsync(ct);
            }

            await using (var delCards = conn.CreateCommand())
            {
                delCards.Transaction = tx;
                delCards.CommandText = @"DELETE FROM cards WHERE id IN (SELECT id FROM _tmp_card_ids);";
                total += await delCards.ExecuteNonQueryAsync(ct);
            }

            await using (var delContainers = conn.CreateCommand())
            {
                delContainers.Transaction = tx;
                delContainers.CommandText = @"DELETE FROM containers WHERE id IN (SELECT id FROM _tmp_cont_ids);";
                total += await delContainers.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return total;
        }

        // ------------------- Helpers -------------------

        private static async Task BuildSubtreeTemps(
            SqliteConnection conn, SqliteTransaction tx, string rootId, CancellationToken ct)
        {
            const string cte = @"
WITH RECURSIVE sub(id) AS (
  SELECT @id
  UNION ALL
  SELECT c.id
  FROM containers c
  JOIN sub s ON c.parent_id = s.id
)
SELECT id FROM sub;";

            // containers temp
            await using (var t1 = conn.CreateCommand())
            {
                t1.Transaction = tx;
                t1.CommandText = @"DROP TABLE IF EXISTS _tmp_cont_ids;
CREATE TEMP TABLE _tmp_cont_ids(id TEXT PRIMARY KEY);";
                await t1.ExecuteNonQueryAsync(ct);
            }

            await using (var f1 = conn.CreateCommand())
            {
                f1.Transaction = tx;
                f1.CommandText = "INSERT INTO _tmp_cont_ids(id) " + cte;
                f1.Parameters.AddWithValue("@id", rootId);
                await f1.ExecuteNonQueryAsync(ct);
            }

            // cards temp
            await using (var t2 = conn.CreateCommand())
            {
                t2.Transaction = tx;
                t2.CommandText = @"
DROP TABLE IF EXISTS _tmp_card_ids;
CREATE TEMP TABLE _tmp_card_ids(id TEXT PRIMARY KEY, parent_id TEXT, name TEXT);
INSERT INTO _tmp_card_ids(id, parent_id, name)
SELECT k.id, k.parent_id, k.name
FROM cards k
JOIN _tmp_cont_ids ci ON k.parent_id = ci.id;";
                await t2.ExecuteNonQueryAsync(ct);
            }
        }

        private static async Task<(string? parentId, string? name)?> GetContainerRestoreConflictAsync(
            SqliteConnection conn, SqliteTransaction tx, CancellationToken ct)
        {
            const string sql = @"
WITH items AS (
  SELECT c.id, c.parent_id, TRIM(LOWER(c.name)) AS nrm
  FROM containers c
  WHERE c.id IN (SELECT id FROM _tmp_cont_ids)
)
SELECT t.parent_id, x.name
FROM items t
JOIN containers x
  ON COALESCE(x.parent_id,'') = COALESCE(t.parent_id,'')
 AND TRIM(LOWER(x.name)) = t.nrm
 AND x.is_deleted = 0
 AND x.id <> t.id
LIMIT 1;";

            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                var parentId = r.IsDBNull(0) ? null : r.GetString(0);
                var name     = r.IsDBNull(1) ? null : r.GetString(1);
                return (parentId, name);
            }

            return null;
        }

        private static async Task<(string? parentId, string? name)?> GetCardRestoreConflictAsync(
            SqliteConnection conn, SqliteTransaction tx, CancellationToken ct)
        {
            const string sql = @"
WITH items AS (
  SELECT id, parent_id, TRIM(LOWER(name)) AS nrm
  FROM _tmp_card_ids
)
SELECT k.parent_id, x.name
FROM items k
JOIN cards x
  ON COALESCE(x.parent_id,'') = COALESCE(k.parent_id,'')
 AND TRIM(LOWER(x.name)) = k.nrm
 AND x.is_deleted = 0
 AND x.id <> k.id
LIMIT 1;";

            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                var parentId = r.IsDBNull(0) ? null : r.GetString(0);
                var name     = r.IsDBNull(1) ? null : r.GetString(1);
                return (parentId, name);
            }

            return null;
        }
    }
}
