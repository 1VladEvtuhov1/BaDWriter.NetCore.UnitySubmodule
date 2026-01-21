using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Tags;
using Database.Abstractions.Queries;
using Database.Abstractions.Repositories;
using Database.Local.Sqlite.Sqlite;
using Microsoft.Data.Sqlite;

namespace Database.Local.Sqlite.Repositories
{
    /// <summary>
    /// SQLite repository for Tags (DTO-first, netstandard2.1).
    /// </summary>
    public sealed class SqliteTagRepository :
        IDocumentRepository<TagDto>,
        ITagQueries
    {
        private readonly ISqliteConnectionFactory _factory;
        public SqliteTagRepository(ISqliteConnectionFactory factory) => _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        // ---------------- IDocumentRepository<TagDto> ----------------

        public async Task<TagDto?> GetAsync(string id, CancellationToken ct = default)
        {
            const string sql = @"
SELECT id, name, color_argb, version, updated_at_utc, is_deleted
FROM tags
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

        public async Task UpsertAsync(TagDto doc, long? expectedVersion = null, CancellationToken ct = default)
        {
            if (doc is null) throw new ArgumentNullException(nameof(doc));
            var id   = string.IsNullOrWhiteSpace(doc.Id) ? Guid.NewGuid().ToString("N") : doc.Id;
            var name = doc.Name ?? string.Empty;
            var norm = NormalizeName(name);

            // конфликт по имени среди живых тегов
            const string chkSql = @"
SELECT 1
FROM tags
WHERE normalized_name = @norm AND is_deleted = 0 AND id <> @id
LIMIT 1;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using (var chk = new SqliteCommand(chkSql, conn))
            {
                chk.Parameters.AddWithValue("@norm", norm);
                chk.Parameters.AddWithValue("@id", id);
                if (await chk.ExecuteScalarAsync(ct) is not null)
                    throw new DuplicateNameException("Tag name conflict: " + name);
            }

            const string upsertSql = @"
INSERT INTO tags (id, name, normalized_name, color_argb, version, updated_at_utc, is_deleted)
VALUES (@id, @name, @norm, @color, COALESCE(@ver, 0), strftime('%s','now'), 0)
ON CONFLICT(id) DO UPDATE SET
    name            = excluded.name,
    normalized_name = excluded.normalized_name,
    color_argb      = excluded.color_argb,
    version         = tags.version + 1,
    updated_at_utc  = excluded.updated_at_utc,
    is_deleted      = 0;";

            await using (var cmd = new SqliteCommand(upsertSql, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@norm", norm);
                cmd.Parameters.AddWithValue("@color", doc.ColorArgb);
                cmd.Parameters.AddWithValue("@ver", (object?)expectedVersion ?? DBNull.Value);

                var rows = await cmd.ExecuteNonQueryAsync(ct);
                if (rows <= 0) throw new InvalidOperationException("Upsert(tag) failed.");
            }
        }

        public async Task<bool> DeleteAsync(string id, long? expectedVersion = null, CancellationToken ct = default)
        {
            const string sql = @"
UPDATE tags
SET is_deleted = 1,
    updated_at_utc = strftime('%s','now'),
    version = version + 1
WHERE id = @id AND is_deleted = 0;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);

            var rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows > 0;
        }

        // ---------------- ITagQueries (DTO) ----------------

        public async Task<TagDto?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default)
        {
            const string sql = @"
SELECT id, name, color_argb, version, updated_at_utc, is_deleted
FROM tags
WHERE normalized_name = @norm AND is_deleted = 0
LIMIT 1;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@norm", normalizedName ?? string.Empty);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;

            return Map(r);
        }

        public async IAsyncEnumerable<TagDto> GetUpdatedSinceAsync(
            long since, string? afterId, int take,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            afterId ??= string.Empty;

            const string sql = @"
SELECT id, name, color_argb, version, updated_at_utc, is_deleted
FROM tags
WHERE (updated_at_utc > @since)
   OR (updated_at_utc = @since AND id > @after)
ORDER BY updated_at_utc, id
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

        public async Task<bool> ExistsByNormalizedNameAsync(string normalizedName, CancellationToken ct = default)
        {
            const string sql = @"SELECT 1 FROM tags WHERE normalized_name = @norm AND is_deleted = 0 LIMIT 1;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@norm", normalizedName ?? string.Empty);

            var val = await cmd.ExecuteScalarAsync(ct);
            return val is not null;
        }

        public async IAsyncEnumerable<TagDto> SearchByTextAsync(
            string text, int skip, int take,
            [EnumeratorCancellation] CancellationToken ct)
        {
            if (skip < 0) skip = 0;
            if (take <= 0) take = 100;
            if (take > 500) take = 500;

            text ??= string.Empty;
            var norm = NormalizeName(text);

            const string sql = @"
SELECT id, name, color_argb, version, updated_at_utc, is_deleted
FROM tags
WHERE is_deleted = 0
  AND (name LIKE @like ESCAPE '\'
       OR normalized_name = @norm)
ORDER BY name
LIMIT @take OFFSET @skip;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@like", "%" + EscapeLike(text) + "%");
            cmd.Parameters.AddWithValue("@norm", norm);
            cmd.Parameters.AddWithValue("@take", take);
            cmd.Parameters.AddWithValue("@skip", skip);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                yield return Map(r);
        }

        // ---------------- helpers ----------------

        private static TagDto Map(IDataRecord r)
        {
            var id        = r.GetString(0);
            var name      = r.GetString(1);
            var color     = Convert.ToInt32(r.GetValue(2));
            var version   = r.GetInt64(3);
            var updated   = r.GetInt64(4);
            var isDeleted = r.GetBoolean(5);

            return new TagDto(
                Id: id,
                Name: name,
                ColorArgb: color,
                Version: version,
                UpdatedAtUtc: updated,
                IsDeleted: isDeleted
            );
        }

        private static string EscapeLike(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
        }

        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var s = name.Trim().ToLowerInvariant();

            var sb = new StringBuilder(s.Length);
            var prevWs = false;
            foreach (var ch in s)
            {
                var isWs = char.IsWhiteSpace(ch);
                if (isWs)
                {
                    if (!prevWs) sb.Append(' ');
                    prevWs = true;
                }
                else
                {
                    sb.Append(ch);
                    prevWs = false;
                }
            }
            return sb.ToString();
        }
    }
}
