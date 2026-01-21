using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Database.Local.Sqlite.Sqlite
{
    public static class SqliteSchema
    {
        public static async Task EnsureCreatedAsync(ISqliteConnectionFactory factory, CancellationToken ct = default)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            await using var conn = factory.Create();
            await conn.OpenAsync(ct);

            await ExecAsync(conn, "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;", ct);

            var sql = @"
CREATE TABLE IF NOT EXISTS worlds (
    id              TEXT PRIMARY KEY,
    name            TEXT NOT NULL,
    description     TEXT NOT NULL,
    version         INTEGER NOT NULL DEFAULT 0,
    updated_at_utc  INTEGER NOT NULL DEFAULT (strftime('%s','now')),
    is_deleted      INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_worlds_updated_id ON worlds(updated_at_utc, id);
CREATE INDEX IF NOT EXISTS ix_worlds_name       ON worlds(name);

CREATE TABLE IF NOT EXISTS containers (
    id              TEXT PRIMARY KEY,
    world_id        TEXT NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
    parent_id       TEXT NULL  REFERENCES containers(id) ON DELETE CASCADE,
    name            TEXT NOT NULL,
    description     TEXT NOT NULL DEFAULT '',
    content_type    INTEGER NOT NULL DEFAULT 1,
    ""order""       INTEGER NOT NULL DEFAULT 0,
    version         INTEGER NOT NULL DEFAULT 0,
    updated_at_utc  INTEGER NOT NULL DEFAULT (strftime('%s','now')),
    is_deleted      INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_containers_world_order ON containers(world_id, ""order"");
CREATE INDEX IF NOT EXISTS ix_containers_parent      ON containers(parent_id, is_deleted);
CREATE INDEX IF NOT EXISTS ix_containers_updated_id  ON containers(updated_at_utc, id);
CREATE INDEX IF NOT EXISTS ix_containers_name        ON containers(name);

CREATE TABLE IF NOT EXISTS cards (
    id                     TEXT PRIMARY KEY,
    parent_id              TEXT NOT NULL REFERENCES containers(id) ON DELETE CASCADE,
    name                   TEXT NOT NULL,
    description            TEXT NOT NULL DEFAULT '',
    art_path TEXT NULL,
    ""order""              INTEGER NOT NULL DEFAULT 0,
    version                INTEGER NOT NULL DEFAULT 0,
    updated_at_utc         INTEGER NOT NULL DEFAULT (strftime('%s','now')),
    is_deleted             INTEGER NOT NULL DEFAULT 0,
    has_layout             INTEGER NOT NULL DEFAULT 0,
    layout_version         INTEGER NULL,
    layout_updated_at_utc  INTEGER NULL,
    variant_of_id          TEXT NULL,
    variant_order          INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_cards_parent        ON cards(parent_id, is_deleted);
CREATE INDEX IF NOT EXISTS ix_cards_updated_id    ON cards(updated_at_utc, id);
CREATE INDEX IF NOT EXISTS ix_cards_name          ON cards(name);
CREATE INDEX IF NOT EXISTS idx_cards_parent_order ON cards(parent_id, ""order"", id);
CREATE INDEX IF NOT EXISTS ix_cards_variant_of_id ON cards(variant_of_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_cards_variant_pair ON cards(variant_of_id, variant_order);

CREATE TABLE IF NOT EXISTS tags (
    id               TEXT PRIMARY KEY,
    name             TEXT NOT NULL,
    normalized_name  TEXT NOT NULL UNIQUE,
    color_argb       INTEGER NOT NULL,
    version          INTEGER NOT NULL DEFAULT 0,
    updated_at_utc   INTEGER NOT NULL DEFAULT (strftime('%s','now')),
    is_deleted       INTEGER NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_tags_normalized ON tags(normalized_name);
CREATE INDEX IF NOT EXISTS ix_tags_updated_id        ON tags(updated_at_utc, id);
CREATE INDEX IF NOT EXISTS ix_tags_name              ON tags(name);

CREATE TABLE IF NOT EXISTS card_tags (
    card_id  TEXT NOT NULL REFERENCES cards(id) ON DELETE CASCADE,
    tag_id   TEXT NOT NULL REFERENCES tags(id)  ON DELETE CASCADE,
    PRIMARY KEY(card_id, tag_id)
);
CREATE INDEX IF NOT EXISTS ix_card_tags_tag  ON card_tags(tag_id);
CREATE INDEX IF NOT EXISTS ix_card_tags_card ON card_tags(card_id);

CREATE TABLE IF NOT EXISTS card_layouts (
  card_id         TEXT    NOT NULL PRIMARY KEY REFERENCES cards(id) ON DELETE CASCADE,
  layout_version  INTEGER NOT NULL DEFAULT 0,
  updated_at_utc  INTEGER NOT NULL,
  is_deleted      INTEGER NOT NULL DEFAULT 0,
  payload_json    TEXT    NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_card_layouts_updated ON card_layouts(updated_at_utc);

CREATE TRIGGER IF NOT EXISTS trg_card_variant_insert
BEFORE INSERT ON cards
FOR EACH ROW
WHEN NEW.variant_of_id IS NOT NULL
BEGIN
  SELECT RAISE(ABORT, 'variant parent not found')
    WHERE NOT EXISTS (SELECT 1 FROM cards p WHERE p.id = NEW.variant_of_id);
  SELECT RAISE(ABORT, 'variant of variant')
    WHERE EXISTS (SELECT 1 FROM cards p WHERE p.id = NEW.variant_of_id AND p.variant_of_id IS NOT NULL);
  SELECT RAISE(ABORT, 'self reference')
    WHERE NEW.variant_of_id = NEW.id;
END;

CREATE TRIGGER IF NOT EXISTS trg_card_variant_update
BEFORE UPDATE OF variant_of_id ON cards
FOR EACH ROW
WHEN NEW.variant_of_id IS NOT NULL
BEGIN
  SELECT RAISE(ABORT, 'variant parent not found')
    WHERE NOT EXISTS (SELECT 1 FROM cards p WHERE p.id = NEW.variant_of_id);
  SELECT RAISE(ABORT, 'variant of variant')
    WHERE EXISTS (SELECT 1 FROM cards p WHERE p.id = NEW.variant_of_id AND p.variant_of_id IS NOT NULL);
  SELECT RAISE(ABORT, 'self reference')
    WHERE NEW.variant_of_id = NEW.id;
END;

CREATE TRIGGER IF NOT EXISTS trg_card_variant_container_ins
BEFORE INSERT ON cards
FOR EACH ROW
WHEN NEW.variant_of_id IS NOT NULL
BEGIN
  SELECT RAISE(ABORT, 'variant container mismatch')
    WHERE EXISTS (
      SELECT 1 FROM cards p
      WHERE p.id = NEW.variant_of_id AND p.parent_id <> NEW.parent_id
    );
END;

CREATE TRIGGER IF NOT EXISTS trg_card_variant_container_upd
BEFORE UPDATE OF parent_id, variant_of_id ON cards
FOR EACH ROW
WHEN NEW.variant_of_id IS NOT NULL
BEGIN
  SELECT RAISE(ABORT, 'variant container mismatch')
    WHERE EXISTS (
      SELECT 1 FROM cards p
      WHERE p.id = NEW.variant_of_id AND p.parent_id <> NEW.parent_id
    );
END;

CREATE TRIGGER IF NOT EXISTS trg_card_variant_parent_delete_hard
AFTER DELETE ON cards
FOR EACH ROW
BEGIN
  DELETE FROM cards WHERE variant_of_id = OLD.id;
END;

CREATE TRIGGER IF NOT EXISTS trg_card_variant_parent_delete_soft
AFTER UPDATE OF is_deleted ON cards
FOR EACH ROW
WHEN NEW.is_deleted = 1 AND OLD.is_deleted <> 1
BEGIN
  UPDATE cards
  SET is_deleted = 1,
      updated_at_utc = strftime('%s','now')
  WHERE variant_of_id = NEW.id AND is_deleted = 0;
END;

-- Containers: world_id consistency with parent + forbid world switches

-- On INSERT: if parent exists, NEW.world_id must equal parent's world_id
CREATE TRIGGER IF NOT EXISTS trg_containers_world_consistency_ins
BEFORE INSERT ON containers
FOR EACH ROW
BEGIN
  -- parent → child world check
  SELECT RAISE(ABORT, 'container world mismatch with parent')
    WHERE NEW.parent_id IS NOT NULL
      AND (SELECT world_id FROM containers WHERE id = NEW.parent_id) <> NEW.world_id;
END;

-- On UPDATE of parent_id/world_id: forbid changing world_id, and check new parent world
CREATE TRIGGER IF NOT EXISTS trg_containers_world_consistency_upd
BEFORE UPDATE OF parent_id, world_id ON containers
FOR EACH ROW
BEGIN
  -- changing world_id is not allowed
  SELECT RAISE(ABORT, 'changing container world_id is not allowed')
    WHERE NEW.world_id <> OLD.world_id;

  -- parent → child world check on move
  SELECT RAISE(ABORT, 'container world mismatch with new parent')
    WHERE NEW.parent_id IS NOT NULL
      AND (SELECT world_id FROM containers WHERE id = NEW.parent_id) <> NEW.world_id;
END;";

            await ExecAsync(conn, sql, ct);
        }

        private static async Task ExecAsync(SqliteConnection conn, string sql, CancellationToken ct)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}