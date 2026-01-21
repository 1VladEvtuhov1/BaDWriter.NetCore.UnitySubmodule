using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using BadWriter.Contracts.Cards;
using Database.Abstractions.Interfaces;
using Database.Abstractions.Queries;
using Database.Abstractions.Repositories;
using Database.Application.Queries;
using Database.Local.Sqlite.Mappers;
using Database.Local.Sqlite.Sqlite;
using Microsoft.Data.Sqlite;

namespace Database.Local.Sqlite.Repositories
{
    public sealed class SqliteCardRepository : IDocumentRepository<CardDto>, ICardQueries, ICardCascadeRepository,
        ICardLayoutMetaRepository, ICardVariantQueries, ICardVariantRepository, ICardArtRepository
    {
        private readonly ISqliteConnectionFactory _factory;
        public SqliteCardRepository(ISqliteConnectionFactory factory) => _factory = factory;

        // ----------------- IDocumentRepository<Card> -----------------

        public async IAsyncEnumerable<CardDto> ListByCardContainerAndTagsAsync(
            string parentId,
            IReadOnlyList<string> tagIds,
            TagMatchMode matchMode,
            int skip,
            int take,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken ct = default)
        {
            if (tagIds == null || tagIds.Count == 0)
            {
                await foreach (var c in ListByCardContainerAsync(parentId, skip, take, ct))
                    yield return c;

                yield break;
            }

            // Keep IDs as-is (they are identifiers, not user input text).
            // Only filter out empty/whitespace and duplicates.
            var filtered = new List<string>(tagIds.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < tagIds.Count; i++)
            {
                var id = tagIds[i];
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (seen.Add(id))
                    filtered.Add(id);
            }

            if (filtered.Count == 0)
            {
                await foreach (var c in ListByCardContainerAsync(parentId, skip, take, ct))
                    yield return c;

                yield break;
            }

            // NOTE: SQLite commonly has a parameter limit (often 999). Practically, tag filters are small.
            var placeholders = new string[filtered.Count];
            for (var i = 0; i < filtered.Count; i++)
                placeholders[i] = $"@t{i}";

            var inList = string.Join(",", placeholders);

            string sql;
            if (matchMode == TagMatchMode.All)
            {
                sql = $@"
SELECT
  {CardSql.SelectColumns("c")}
FROM cards c
WHERE c.parent_id = @pid AND c.is_deleted = 0
  AND c.id IN (
    SELECT ct.card_id
    FROM card_tags ct
    JOIN tags t ON t.id = ct.tag_id AND t.is_deleted = 0
    WHERE ct.tag_id IN ({inList})
    GROUP BY ct.card_id
    HAVING COUNT(DISTINCT ct.tag_id) = @tagCount
  )
ORDER BY c.""order"" ASC, c.id ASC
LIMIT @take OFFSET @skip;";
            }
            else
            {
                sql = $@"
SELECT
  {CardSql.SelectColumns("c")}
FROM cards c
WHERE c.parent_id = @pid AND c.is_deleted = 0
  AND EXISTS (
    SELECT 1
    FROM card_tags ct
    JOIN tags t ON t.id = ct.tag_id AND t.is_deleted = 0
    WHERE ct.card_id = c.id
      AND ct.tag_id IN ({inList})
  )
ORDER BY c.""order"" ASC, c.id ASC
LIMIT @take OFFSET @skip;";
            }

            await using var conn = _factory.Create(); // factory opens connection
            await using var cmd = new SqliteCommand(sql, conn);

            cmd.Parameters.AddWithValue("@pid", parentId);
            cmd.Parameters.AddWithValue("@take", take);
            cmd.Parameters.AddWithValue("@skip", skip);

            if (matchMode == TagMatchMode.All)
                cmd.Parameters.AddWithValue("@tagCount", filtered.Count);

            for (var i = 0; i < filtered.Count; i++)
                cmd.Parameters.AddWithValue($"@t{i}", filtered[i]);

            await using var r = await cmd.ExecuteReaderAsync(ct);

            CardOrdinals ord = default!;
            var first = true;

            while (await r.ReadAsync(ct))
            {
                if (first)
                {
                    ord = new CardOrdinals(r);
                    first = false;
                }

                yield return CardRowMapper.MapDto(r, ord);
            }
        }

        public async Task<CardDto> GetAsync(string id, CancellationToken ct = default)
        {
            var sql = $@"
SELECT
  {CardSql.SelectColumns("c")}
FROM cards c
WHERE c.id = @id AND c.is_deleted = 0;";

            await using var conn = _factory.Create(); // factory opens connection
            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
                return null;

            var ord = new CardOrdinals(r);
            var card = CardRowMapper.MapDto(r, ord);
            return card;
        }

        public async IAsyncEnumerable<CardDto> GetUpdatedSinceAsync(
            long since, string? afterId, int take,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken ct = default)
        {
            afterId ??= string.Empty;
            var tk = Math.Max(1, take);

            var sql = $@"
SELECT
  {CardSql.SelectColumns("c")}
FROM cards c
WHERE c.is_deleted = 0
  AND (c.updated_at_utc > @since
       OR (c.updated_at_utc = @since AND c.id > @after))
ORDER BY c.updated_at_utc ASC, c.id ASC
LIMIT @take;";

            await using var conn = _factory.Create();
            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@since", since);
            cmd.Parameters.AddWithValue("@after", afterId);
            cmd.Parameters.AddWithValue("@take", tk);

            await using var r = await cmd.ExecuteReaderAsync(ct);

            CardOrdinals ord = default!;
            var first = true;

            while (await r.ReadAsync(ct))
            {
                if (first)
                {
                    ord = new CardOrdinals(r);
                    first = false;
                }

                yield return CardRowMapper.MapDto(r, ord);
            }
        }

        public async Task<bool> ExistsByNameAsync(string parentId, string name, string? excludeId = null,
            CancellationToken ct = default)
        {
            const string sql = @"
SELECT 1
FROM cards
WHERE parent_id = @pid
  AND LOWER(name) = LOWER(@name)
  AND is_deleted = 0
  AND (@ex IS NULL OR id <> @ex)
LIMIT 1;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@pid", parentId);
            cmd.Parameters.AddWithValue("@name", name ?? string.Empty);
            cmd.Parameters.AddWithValue("@ex", (object?)excludeId ?? DBNull.Value);

            var exists = await cmd.ExecuteScalarAsync(ct);
            return exists is not null;
        }

        public async Task UpsertAsync(CardDto doc, long? expectedVersion = null, CancellationToken ct = default)
        {
            if (doc is null) throw new ArgumentNullException(nameof(doc));
            if (string.IsNullOrWhiteSpace(doc.ParentId))
                throw new ArgumentException("parentId is required", nameof(doc.ParentId));
            if (string.IsNullOrWhiteSpace(doc.Name))
                throw new ArgumentException("name is required", nameof(doc.Name));

            var id = string.IsNullOrWhiteSpace(doc.Id) ? Guid.NewGuid().ToString("N") : doc.Id;
            var pid = doc.ParentId!;
            var name = doc.Name;
            var desc = doc.Description ?? string.Empty;
            var art = (object?)doc.ArtPath ?? DBNull.Value;

            // если нужно хранить порядок карточек внутри контейнера — сейчас ставим 0 (в DTO свойства нет)
            const int ord = 0;

            const string checkSql = @"
SELECT 1 FROM cards
WHERE parent_id = @pid
  AND LOWER(name) = LOWER(@name)
  AND is_deleted = 0
  AND id <> @id
LIMIT 1;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using (var checkCmd = new SqliteCommand(checkSql, conn))
            {
                checkCmd.Parameters.AddWithValue("@pid", pid);
                checkCmd.Parameters.AddWithValue("@name", name);
                checkCmd.Parameters.AddWithValue("@id", id);
                var exists = await checkCmd.ExecuteScalarAsync(ct);
                if (exists is not null)
                    throw new DuplicateNameException($"Card; pid='{pid}', name='{name}'");
            }

            const string sql = @"
INSERT INTO cards (id, parent_id, name, description, art_path, ""order"",
                   version, updated_at_utc, is_deleted)
VALUES (@id, @pid, @name, @desc, @art, @ord,
        COALESCE(@ver, 0), strftime('%s','now'), 0)
ON CONFLICT(id) DO UPDATE SET
    parent_id       = excluded.parent_id,
    name            = excluded.name,
    description     = excluded.description,
    art_path        = excluded.art_path,
    ""order""        = excluded.""order"",
    version         = cards.version + 1,
    updated_at_utc  = excluded.updated_at_utc,
    is_deleted      = 0;";

            await using (var cmd = new SqliteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@pid", pid);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@desc", desc);
                cmd.Parameters.AddWithValue("@art", art);
                cmd.Parameters.AddWithValue("@ord", ord);
                cmd.Parameters.AddWithValue("@ver", (object?)expectedVersion ?? DBNull.Value);

                var rows = await cmd.ExecuteNonQueryAsync(ct);
                if (rows <= 0) throw new InvalidOperationException("Upsert(card) failed.");
            }

            // Чтение метаданных обратно не делаем — вызывающая сторона при необходимости перечитает GetAsync.
        }

        public async Task<bool> DeleteAsync(string id, long? expectedVersion = null, CancellationToken ct = default)
        {
            // expectedVersion можно учесть в WHERE при желании; пока мягко удаляем
            const string sql = @"
UPDATE cards
SET is_deleted     = 1,
    updated_at_utc = strftime('%s','now'),
    version        = version + 1
WHERE id = @id AND is_deleted = 0;";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);

            var rows = await cmd.ExecuteNonQueryAsync(ct);
            return rows > 0;
        }

        public async IAsyncEnumerable<CardDto> ListByCardContainerAsync(
            string parentId, int skip, int take,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken ct = default)
        {
            var sql = $@"
SELECT
  {CardSql.SelectColumns("c")}
FROM cards c
WHERE c.parent_id = @pid AND c.is_deleted = 0
ORDER BY c.""order"" ASC, c.id ASC
LIMIT @take OFFSET @skip;";

            await using var conn = _factory.Create();
            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@pid", parentId);
            cmd.Parameters.AddWithValue("@take", take);
            cmd.Parameters.AddWithValue("@skip", skip);

            await using var r = await cmd.ExecuteReaderAsync(ct);

            CardOrdinals ord = default!;
            var first = true;

            while (await r.ReadAsync(ct))
            {
                if (first)
                {
                    ord = new CardOrdinals(r);
                    first = false;
                }

                yield return CardRowMapper.MapDto(r, ord);
            }
        }

        public async Task<int> PurgeAsync(string id, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Id is required.", nameof(id));

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

            var total = 0;

            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM card_tags WHERE card_id=@id;";
                cmd.Parameters.AddWithValue("@id", id);
                total += await cmd.ExecuteNonQueryAsync(ct);
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM cards WHERE id=@id;";
                cmd.Parameters.AddWithValue("@id", id);
                total += await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return total;
        }

        public async Task UpdateLayoutMetaAsync(
            string cardId, bool hasLayout, long? layoutVersion, long? layoutUpdatedAtUtc, CancellationToken ct)
        {
            await using var conn = _factory.Create();

            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync(ct);

            const string sql = @"
UPDATE cards
SET has_layout = @has,
    layout_version = @ver,
    layout_updated_at_utc = @ts
WHERE id = @id;";

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(new SqliteParameter("@has", hasLayout ? 1 : 0));
            cmd.Parameters.Add(new SqliteParameter("@ver", (object?)layoutVersion ?? DBNull.Value));
            cmd.Parameters.Add(new SqliteParameter("@ts", (object?)layoutUpdatedAtUtc ?? DBNull.Value));
            cmd.Parameters.Add(new SqliteParameter("@id", cardId));

            var affected = await cmd.ExecuteNonQueryAsync(ct);
            if (affected == 0)
                throw new InvalidOperationException($"Card '{cardId}' not found.");
        }

        #region --- Variants (ICardVariantQueries/ICardVariantRepository) ---

        public async Task<IReadOnlyList<CardDto>> GetVariantsAsync(string parentCardId, CancellationToken ct = default)
        {
            const string sql = @"
SELECT  id, parent_id, name, description, art_path, ""order"",
        version, updated_at_utc, is_deleted,
        has_layout, layout_version, layout_updated_at_utc,
        variant_of_id, variant_order
FROM cards
WHERE is_deleted = 0 AND variant_of_id = @pid
ORDER BY variant_order ASC, id ASC;";

            await using var conn = _factory.Create();
            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@pid", parentCardId);

            var list = new List<CardDto>();
            await using var r = await cmd.ExecuteReaderAsync(ct);

            while (await r.ReadAsync(ct))
                list.Add(ReadCardWithVariants(r));

            return list;
        }

        public async Task<IReadOnlyList<CardDto>> GetGroupAsync(string anyCardId, CancellationToken ct = default)
        {
            const string sql = @"
WITH grp AS (
    SELECT COALESCE(variant_of_id, id) AS gid FROM cards WHERE id = @id
)
SELECT  c.id, c.parent_id, c.name, c.description, c.art_path, c.""order"",
        c.version, c.updated_at_utc, c.is_deleted,
        c.has_layout, c.layout_version, c.layout_updated_at_utc,
        c.variant_of_id, c.variant_order
FROM cards c, grp
WHERE c.is_deleted = 0
  AND COALESCE(c.variant_of_id, c.id) = grp.gid
ORDER BY (c.variant_of_id IS NULL) DESC, c.variant_order ASC, c.id ASC;";

            await using var conn = _factory.Create();
            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", anyCardId);

            var list = new List<CardDto>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add(ReadCardWithVariants(r));

            return list;
        }

        public async Task<CardDto> CreateVariantAsync(string parentCardId, CardDto variant,
            CancellationToken ct = default)
        {
            if (variant is null) throw new ArgumentNullException(nameof(variant));
            if (string.IsNullOrWhiteSpace(parentCardId))
                throw new ArgumentException("parentCardId is required.", nameof(parentCardId));
            if (string.IsNullOrWhiteSpace(variant.ParentId))
                throw new ArgumentException("variant.ParentId is required.");

            const string sqlMax = @"SELECT COALESCE(MAX(variant_order), -1) FROM cards WHERE variant_of_id = @pid;";
            const string sqlIns = @"
INSERT INTO cards (id, parent_id, name, description, art_path, ""order"",
                   version, updated_at_utc, is_deleted,
                   has_layout, layout_version, layout_updated_at_utc,
                   variant_of_id, variant_order)
VALUES (@id, @parent_id, @name, @desc, @art, @order,
        0, strftime('%s','now'), 0,
        @has_layout, @layout_ver, @layout_ts,
        @variant_of_id, @variant_order);";

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

            int maxOrd;
            await using (var c1 = new SqliteCommand(sqlMax, conn, tx))
            {
                c1.Parameters.AddWithValue("@pid", parentCardId);
                maxOrd = Convert.ToInt32(await c1.ExecuteScalarAsync(ct));
            }

            var id = string.IsNullOrWhiteSpace(variant.Id) ? Guid.NewGuid().ToString("N") : variant.Id;
            var pId = variant.ParentId!;
            var name = variant.Name ?? string.Empty;
            var desc = variant.Description ?? string.Empty;
            var art = (object?)variant.ArtPath ?? DBNull.Value;

            await using (var c2 = new SqliteCommand(sqlIns, conn, tx))
            {
                c2.Parameters.AddWithValue("@id", id);
                c2.Parameters.AddWithValue("@parent_id", pId);
                c2.Parameters.AddWithValue("@name", name);
                c2.Parameters.AddWithValue("@desc", desc);
                c2.Parameters.AddWithValue("@art", art);

                // порядок внутри контейнера — пока 0 (в DTO нет отдельного поля для него)
                c2.Parameters.AddWithValue("@order", 0);

                c2.Parameters.AddWithValue("@has_layout", variant.HasLayout ? 1 : 0);
                c2.Parameters.AddWithValue("@layout_ver", (object?)variant.LayoutVersion ?? DBNull.Value);
                c2.Parameters.AddWithValue("@layout_ts", (object?)variant.LayoutUpdatedAtUtc ?? DBNull.Value);

                c2.Parameters.AddWithValue("@variant_of_id", parentCardId);
                c2.Parameters.AddWithValue("@variant_order", maxOrd + 1);

                var rows = await c2.ExecuteNonQueryAsync(ct);
                if (rows != 1) throw new InvalidOperationException("CreateVariant failed.");
            }

            await tx.CommitAsync(ct);

            // Возвращаем DTO, совместимый с остальным кодом
            return new CardDto(
                Id: id,
                Name: name,
                ParentId: pId,
                ArtPath: variant.ArtPath,
                Description: desc,
                TagIds: Array.Empty<string>(),
                Version: 0,
                UpdatedAtUtc: 0,
                IsDeleted: false,
                HasLayout: variant.HasLayout,
                LayoutUpdatedAtUtc: variant.LayoutUpdatedAtUtc,
                LayoutVersion: variant.LayoutVersion,
                VariantOfId: parentCardId,
                VariantOrder: maxOrd + 1
            );
        }

        public async Task ReorderVariantsAsync(string parentCardId, IReadOnlyList<(string CardId, int Order)> newOrder,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(parentCardId))
                throw new ArgumentException("parentCardId is required.", nameof(parentCardId));
            if (newOrder is null || newOrder.Count == 0)
                return;

            const string countSql = @"SELECT COUNT(*) FROM cards WHERE variant_of_id = @pid AND is_deleted = 0;";
            const string pivotSql =
                @"UPDATE cards SET variant_order = -(variant_order + 1) WHERE variant_of_id = @pid;";
            const string setSql = @"UPDATE cards SET variant_order = @ord WHERE id = @id AND variant_of_id = @pid;";

            await using var conn = _factory.Create();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
            await using var tx = (Microsoft.Data.Sqlite.SqliteTransaction)await conn.BeginTransactionAsync(ct);

            int total;
            await using (var c0 = new Microsoft.Data.Sqlite.SqliteCommand(countSql, conn, tx))
            {
                c0.Parameters.AddWithValue("@pid", parentCardId);
                total = Convert.ToInt32(await c0.ExecuteScalarAsync(ct));
            }

            if (newOrder.Count != total)
                throw new InvalidOperationException("newOrder must include all variants of the parent.");

            await using (var pivot = new Microsoft.Data.Sqlite.SqliteCommand(pivotSql, conn, tx))
            {
                pivot.Parameters.AddWithValue("@pid", parentCardId);
                await pivot.ExecuteNonQueryAsync(ct);
            }

            foreach (var (cardId, order) in newOrder)
            {
                await using var cmd = new Microsoft.Data.Sqlite.SqliteCommand(setSql, conn, tx);
                cmd.Parameters.AddWithValue("@ord", Math.Max(0, order));
                cmd.Parameters.AddWithValue("@id", cardId);
                cmd.Parameters.AddWithValue("@pid", parentCardId);
                var n = await cmd.ExecuteNonQueryAsync(ct);
                if (n != 1)
                    throw new InvalidOperationException($"Card '{cardId}' is not a variant of '{parentCardId}'.");
            }

            await tx.CommitAsync(ct);
        }

        public async Task DeleteVariantAsync(string variantCardId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(variantCardId))
                throw new ArgumentException("Variant ID cannot be null or empty", nameof(variantCardId));

            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
        UPDATE cards
        SET is_deleted = 1,
            updated_at_utc = strftime('%s','now')
        WHERE id = $id;
    ";

            cmd.Parameters.AddWithValue("$id", variantCardId);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        private static CardDto ReadCardWithVariants(IDataRecord r)
        {
            // Required
            var id = r.GetString(r.GetOrdinal("id"));
            var pid = r.GetString(r.GetOrdinal("parent_id"));
            var name = r.GetString(r.GetOrdinal("name"));
            var desc = r.GetString(r.GetOrdinal("description"));
            var ver = r.GetInt64(r.GetOrdinal("version"));
            var ts = r.GetInt64(r.GetOrdinal("updated_at_utc"));
            var del = r.GetBoolean(r.GetOrdinal("is_deleted"));

            // Optional meta
            var art = SafeGetStringNullable(r, "art_path");
            var hasLayout = SafeGetInt(r, "has_layout") == 1;
            var layVer = SafeGetLongNullable(r, "layout_version");
            var layTs = SafeGetLongNullable(r, "layout_updated_at_utc");

            // Variants
            var varOf = SafeGetStringNullable(r, "variant_of_id");
            var varOrd = Math.Max(0, SafeGetInt(r, "variant_order"));

            // Теги в этом запросе не читаем
            var tags = Array.Empty<string>();

            return new CardDto(
                Id: id,
                Name: name,
                ParentId: pid,
                ArtPath: art,
                Description: desc,
                TagIds: tags,
                Version: ver,
                UpdatedAtUtc: ts,
                IsDeleted: del,
                HasLayout: hasLayout,
                LayoutUpdatedAtUtc: layTs,
                LayoutVersion: layVer,
                VariantOfId: string.IsNullOrWhiteSpace(varOf) ? null : varOf,
                VariantOrder: varOrd
            );

            static int SafeGetInt(IDataRecord rec, string col)
                => rec.GetOrdinal(col) is var i && i >= 0 && !rec.IsDBNull(i) ? Convert.ToInt32(rec.GetValue(i)) : 0;

            static long? SafeGetLongNullable(IDataRecord rec, string col)
                => rec.GetOrdinal(col) is var i && i >= 0 && !rec.IsDBNull(i)
                    ? Convert.ToInt64(rec.GetValue(i))
                    : (long?)null;

            static string? SafeGetStringNullable(IDataRecord rec, string col)
                => rec.GetOrdinal(col) is var i && i >= 0 && !rec.IsDBNull(i) ? rec.GetString(i) : null;
        }

        #endregion

        public async Task UpdateArtPathAsync(string cardId, string? artPath, CancellationToken ct)
        {
            await using var conn = _factory.Create();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);

            const string sql = @"
UPDATE cards
SET art_path       = @art,
    updated_at_utc = strftime('%s','now'),
    version        = version + 1
WHERE id = @id AND is_deleted = 0;";

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@art", (object?)artPath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", cardId);

            var n = await cmd.ExecuteNonQueryAsync(ct);
            if (n == 0)
                throw new KeyNotFoundException($"Card '{cardId}' not found.");
        }
    }
}