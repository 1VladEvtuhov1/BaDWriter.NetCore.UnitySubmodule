using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Blocks;
using BadWriter.Contracts.Content;
using Database.Abstractions.Queries;
using Database.Abstractions.Repositories;
using Database.Local.Sqlite.Sqlite;
using Microsoft.Data.Sqlite;

namespace Database.Local.Sqlite.Repositories
{
    public sealed class SqliteBlockTemplateRepository :
        IDocumentRepository<BlockTemplateDto>,
        IBlockTemplateQueries
    {
        private const int DefaultTake = 100;
        private const int MaxTake = 500;

        private readonly ISqliteConnectionFactory _factory;

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public SqliteBlockTemplateRepository(ISqliteConnectionFactory factory)
            => _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        public async Task<BlockTemplateDto?> GetAsync(string id, CancellationToken ct = default)
        {
            const string sql = @"
SELECT id, name, payload_json, version, updated_at_utc, is_deleted
FROM block_templates
WHERE id = @id AND is_deleted = 0
LIMIT 1;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@id", id);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;

            return Map(r);
        }

        public async Task<BlockTemplateDto?> GetAnyAsync(string id, CancellationToken ct = default)
        {
            const string sql = @"
SELECT id, name, payload_json, version, updated_at_utc, is_deleted
FROM block_templates
WHERE id = @id
LIMIT 1;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@id", id);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;

            return Map(r);
        }

        public async IAsyncEnumerable<BlockTemplateDto> ListAsync(
            int skip, int take,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            const string sql = @"
SELECT id, name, payload_json, version, updated_at_utc, is_deleted
FROM block_templates
WHERE is_deleted = 0
ORDER BY name, id
LIMIT @take OFFSET @skip;";

            await foreach (var x in Query(sql, _ => { }, skip, take, ct))
                yield return x;
        }

        public async IAsyncEnumerable<BlockTemplateDto> GetUpdatedSinceAsync(
            long since, string? afterId, int take,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            afterId ??= string.Empty;

            const string sql = @"
SELECT id, name, payload_json, version, updated_at_utc, is_deleted
FROM block_templates
WHERE (updated_at_utc > @since) OR (updated_at_utc = @since AND id > @after)
ORDER BY updated_at_utc, id
LIMIT @take;";

            await foreach (var x in Query(
                               sql,
                               p =>
                               {
                                   p.AddWithValue("@since", since);
                                   p.AddWithValue("@after", afterId);
                               },
                               skip: 0, take: take, ct))
                yield return x;
        }

        public async Task<bool> ExistsByNameAsync(string name, string? excludeId = null, CancellationToken ct = default)
        {
            const string sql = @"
SELECT 1
FROM block_templates
WHERE TRIM(LOWER(name)) = TRIM(LOWER(@name))
  AND is_deleted = 0
  AND (@ex IS NULL OR id <> @ex)
LIMIT 1;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@name", name ?? string.Empty);
            cmd.Parameters.AddWithValue("@ex", (object?)excludeId ?? DBNull.Value);

            var exists = await cmd.ExecuteScalarAsync(ct);
            return exists is not null;
        }

        public async Task UpsertAsync(BlockTemplateDto doc, long? expectedVersion = null, CancellationToken ct = default)
        {
            if (doc is null) throw new ArgumentNullException(nameof(doc));
            if (string.IsNullOrWhiteSpace(doc.Id)) throw new ArgumentException("id is required", nameof(doc.Id));
            if (string.IsNullOrWhiteSpace(doc.Name)) throw new ArgumentException("name is required", nameof(doc.Name));
            if (doc.Payload is null) throw new ArgumentException("payload is required", nameof(doc.Payload));

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            const string sql = @"
INSERT INTO block_templates (id, name, payload_json, version, updated_at_utc, is_deleted)
VALUES (@id, @name, @json, COALESCE(@ver,0), strftime('%s','now'), 0)
ON CONFLICT(id) DO UPDATE SET
  name           = excluded.name,
  payload_json   = excluded.payload_json,
  version        = block_templates.version + 1,
  updated_at_utc = excluded.updated_at_utc,
  is_deleted     = 0;";

            var payloadJson = JsonSerializer.Serialize(doc.Payload, JsonOpts);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@id", doc.Id);
            cmd.Parameters.AddWithValue("@name", doc.Name);
            cmd.Parameters.AddWithValue("@json", payloadJson);
            cmd.Parameters.AddWithValue("@ver", (object?)expectedVersion ?? DBNull.Value);

            var rows = await cmd.ExecuteNonQueryAsync(ct);
            if (rows <= 0) throw new InvalidOperationException("Upsert(block_template) failed.");
        }

        public async Task<bool> DeleteAsync(string id, long? expectedVersion = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("id is required", nameof(id));

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            const string sql = @"
UPDATE block_templates
SET is_deleted = 1,
    updated_at_utc = strftime('%s','now'),
    version = version + 1
WHERE id = @id AND is_deleted = 0;";

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@id", id);

            var rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows > 0;
        }

        private async IAsyncEnumerable<BlockTemplateDto> Query(
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

            if (sql.Contains("@take"))
            {
                cmd.Parameters.AddWithValue("@take", take);
                cmd.Parameters.AddWithValue("@skip", skip);
            }
            else
            {
                cmd.Parameters.AddWithValue("@take", take);
            }

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                yield return Map(r);
        }

        private static BlockTemplateDto Map(IDataRecord r)
        {
            var id = r.GetString(0);
            var name = r.GetString(1);
            var payloadJson = r.GetString(2);
            var version = r.GetInt64(3);
            var updated = r.GetInt64(4);
            var isDeleted = r.GetBoolean(5);

            var payload = JsonSerializer.Deserialize<BlockDto>(payloadJson, JsonOpts) ?? new BlockDto
            {
                Id = id,
                Children = new List<ElementDto>()
            };

            if (string.IsNullOrWhiteSpace(payload.Id))
                payload = new BlockDto
                {
                    Id = id,
                    Children = payload.Children ?? new List<ElementDto>(),
                    DesignAspectRatio = payload.DesignAspectRatio,
                    PaddingPx = payload.PaddingPx
                };

            return new BlockTemplateDto(
                Id: id,
                Name: name,
                Payload: payload,
                Version: version,
                UpdatedAtUtc: updated,
                IsDeleted: isDeleted
            );
        }
    }
}
