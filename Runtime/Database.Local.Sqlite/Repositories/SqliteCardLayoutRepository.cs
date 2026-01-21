using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Content;
using Database.Abstractions.Interfaces;
using Database.Local.Sqlite.Sqlite;
using Microsoft.Data.Sqlite;

namespace Database.Local.Sqlite.Repositories
{
    public sealed class SqliteCardLayoutRepository : ICardLayoutRepository
    {
        private readonly ISqliteConnectionFactory _factory;

        // Для netstandard2.1 (System.Text.Json 4.7.2) задаём опции вручную
        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // при необходимости можно добавить:
            // DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            // WriteIndented = false
        };

        public SqliteCardLayoutRepository(ISqliteConnectionFactory factory)
            => _factory = factory;

        public async Task<CardLayoutDto?> GetByCardIdAsync(string cardId, CancellationToken ct)
        {
            await using var conn = _factory.Create();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT payload_json FROM card_layouts WHERE card_id = @id AND is_deleted = 0;";
            cmd.Parameters.Add(new SqliteParameter("@id", cardId));

            var json = (string?)await cmd.ExecuteScalarAsync(ct);
            return json is null ? null : JsonSerializer.Deserialize<CardLayoutDto>(json, JsonOpts);
        }

        public async Task UpsertAsync(CardLayoutDto layout, CancellationToken ct)
        {
            await using var conn = _factory.Create();

            const string sql = @"
INSERT INTO card_layouts(card_id, layout_version, updated_at_utc, is_deleted, payload_json)
VALUES(@card_id, @ver, @ts, 0, @json)
ON CONFLICT(card_id) DO UPDATE SET
  layout_version = excluded.layout_version,
  updated_at_utc = excluded.updated_at_utc,
  is_deleted     = 0,
  payload_json   = excluded.payload_json;";

            var json = JsonSerializer.Serialize(layout, JsonOpts);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(new SqliteParameter("@card_id", layout.CardId));
            cmd.Parameters.Add(new SqliteParameter("@ver", layout.LayoutVersion));
            cmd.Parameters.Add(new SqliteParameter("@ts", layout.UpdatedAtUtc));
            cmd.Parameters.Add(new SqliteParameter("@json", json));

            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task SoftDeleteAsync(string cardId, long updatedAtUtc, CancellationToken ct)
        {
            await using var conn = _factory.Create();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE card_layouts SET is_deleted = 1, updated_at_utc = @ts WHERE card_id = @id;";
            cmd.Parameters.Add(new SqliteParameter("@id", cardId));
            cmd.Parameters.Add(new SqliteParameter("@ts", updatedAtUtc));
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task<IReadOnlyList<CardLayoutDto>> GetUpdatedSinceAsync(long sinceUtc, int limit, CancellationToken ct)
        {
            await using var conn = _factory.Create();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT payload_json
FROM card_layouts
WHERE updated_at_utc > @since
ORDER BY updated_at_utc ASC
LIMIT @lim;";
            cmd.Parameters.Add(new SqliteParameter("@since", sinceUtc));
            cmd.Parameters.Add(new SqliteParameter("@lim", limit));

            var list = new List<CardLayoutDto>(limit);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var json = reader.GetString(0);
                var dto  = JsonSerializer.Deserialize<CardLayoutDto>(json, JsonOpts);
                if (dto != null) list.Add(dto);
            }

            return list;
        }
    }
}
