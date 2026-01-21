using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Database.Abstractions.Repositories;
using Database.Local.Sqlite.Sqlite;
using Microsoft.Data.Sqlite;

namespace Database.Local.Sqlite.Repositories
{
    public sealed class SqliteCardTagsRepository : ICardTagsRepository
    {
        private readonly ISqliteConnectionFactory _factory;

        public SqliteCardTagsRepository(ISqliteConnectionFactory factory)
        {
            _factory = factory ?? throw new System.ArgumentNullException(nameof(factory));
        }

        public async Task ReplaceAsync(string cardId, IReadOnlyCollection<string> tagIds,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("cardId is required", nameof(cardId));
            if (tagIds is null)
                throw new ArgumentNullException(nameof(tagIds));

            var distinct = tagIds
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

            await using (var chkCard = conn.CreateCommand())
            {
                chkCard.Transaction = tx;
                chkCard.CommandText = "SELECT 1 FROM cards WHERE id=@id AND is_deleted=0 LIMIT 1;";
                chkCard.Parameters.AddWithValue("@id", cardId);
                if (await chkCard.ExecuteScalarAsync(ct) is null)
                    throw new KeyNotFoundException($"Card '{cardId}' not found.");
            }

            if (distinct.Length > 0)
            {
                var p = string.Join(",", Enumerable.Range(0, distinct.Length).Select(i => $"@t{i}"));
                await using var chkTags = conn.CreateCommand();
                chkTags.Transaction = tx;
                chkTags.CommandText = $"SELECT COUNT(*) FROM tags WHERE id IN ({p}) AND is_deleted=0;";
                for (int i = 0; i < distinct.Length; i++)
                    chkTags.Parameters.AddWithValue($"@t{i}", distinct[i]);

                var cnt = Convert.ToInt32(await chkTags.ExecuteScalarAsync(ct));
                if (cnt != distinct.Length)
                    throw new InvalidOperationException(
                        "One or more tags do not exist or are deleted.");
            }

            await using (var del = conn.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM card_tags WHERE card_id=@cid;";
                del.Parameters.AddWithValue("@cid", cardId);
                await del.ExecuteNonQueryAsync(ct);
            }

            if (distinct.Length > 0)
            {
                await using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = "INSERT INTO card_tags(card_id, tag_id) VALUES (@cid, @tid);";
                ins.Parameters.AddWithValue("@cid", cardId);
                var pTag = ins.Parameters.Add("@tid", SqliteType.Text);

                foreach (var tagId in distinct)
                {
                    pTag.Value = tagId;
                    await ins.ExecuteNonQueryAsync(ct);
                }
            }

            await tx.CommitAsync(ct);
        }


        public async Task<IReadOnlyList<string>> GetTagIdsAsync(string cardId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("cardId is required", nameof(cardId));

            var result = new List<string>(8);

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
        SELECT ct.tag_id
        FROM card_tags ct
        JOIN tags t ON t.id = ct.tag_id
        WHERE ct.card_id=@cid AND t.is_deleted=0
        ORDER BY ct.tag_id;";
            cmd.Parameters.AddWithValue("@cid", cardId);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                result.Add(r.GetString(0));

            return result;
        }

        public async Task<Dictionary<string, string[]>> GetTagIdsForCardsAsync(IReadOnlyList<string> cardIds,
            CancellationToken ct = default)
        {
            if (cardIds is null || cardIds.Count == 0)
                return new Dictionary<string, string[]>(0);

            var result = cardIds.Distinct().ToDictionary(k => k, _ => new List<string>());

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            var names = result.Keys.ToArray();
            var placeholders = string.Join(",", Enumerable.Range(0, names.Length).Select(i => $"@c{i}"));

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
        SELECT ct.card_id, ct.tag_id
        FROM card_tags ct
        JOIN tags t ON t.id = ct.tag_id
        WHERE ct.card_id IN ({placeholders}) AND t.is_deleted=0
        ORDER BY ct.card_id, ct.tag_id;";

            for (int i = 0; i < names.Length; i++)
                cmd.Parameters.AddWithValue($"@c{i}", names[i]);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                result[r.GetString(0)].Add(r.GetString(1));

            return result.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
        }

        public async Task RemoveTagEverywhereAsync(string tagId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tagId))
                throw new System.ArgumentException("tagId is required", nameof(tagId));

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM card_tags WHERE tag_id=@tid;";
            cmd.Parameters.AddWithValue("@tid", tagId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}