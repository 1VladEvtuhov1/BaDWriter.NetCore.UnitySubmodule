using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BadWriter.Contracts.Cards;

namespace Database.Local.Sqlite.Mappers
{
    public static class CardRowMapper
    {
        /// <summary>
        /// Maps a data record to CardDto. Tag ids are optional and usually fetched separately.
        /// </summary>
        public static CardDto MapDto(IDataRecord r, CardOrdinals o, IReadOnlyList<string>? tagIds = null)
        {
            var id        = r.GetString(o.Id);
            var parentId  = r.GetString(o.ParentId);
            var name      = r.GetString(o.Name);
            var desc      = r.IsDBNull(o.Description) ? string.Empty : r.GetString(o.Description);
            var order     = r.GetInt32(o.Order); // учтён ниже в VariantOrder при необходимости

            var version   = r.GetInt64(o.Version);
            var updated   = r.GetInt64(o.UpdatedAtUtc);
            var isDeleted = r.GetBoolean(o.IsDeleted);

            var hasLayout   = o.HasLayout >= 0 && !r.IsDBNull(o.HasLayout) && r.GetInt32(o.HasLayout) != 0;
            long? layoutVer = o.LayoutVersion     >= 0 && !r.IsDBNull(o.LayoutVersion)     ? r.GetInt64(o.LayoutVersion)     : (long?)null;
            long? layoutTs  = o.LayoutUpdatedAtUtc>= 0 && !r.IsDBNull(o.LayoutUpdatedAtUtc)? r.GetInt64(o.LayoutUpdatedAtUtc) : (long?)null;

            string? art = o.ArtPath >= 0 && !r.IsDBNull(o.ArtPath) ? r.GetString(o.ArtPath) : null;

            string? variantOfId = null;
            var     variantOrd  = 0;
            if (o.VariantOfId >= 0 && !r.IsDBNull(o.VariantOfId))
            {
                variantOfId = r.GetString(o.VariantOfId);
                if (o.VariantOrder >= 0 && !r.IsDBNull(o.VariantOrder))
                    variantOrd = Math.Max(0, r.GetInt32(o.VariantOrder));
            }

            // Если репозиторий не подставляет теги здесь — отдаём пустой массив,
            // контроллер потом подольёт их отдельным запросом.
            var tags = tagIds is null ? Array.Empty<string>()
                                      : (tagIds as string[] ?? tagIds.ToArray());

            return new CardDto(
                id,
                name,
                parentId,
                art,
                desc,
                tags,
                version,
                updated,
                isDeleted,
                hasLayout,
                layoutTs,
                layoutVer,
                variantOfId,
                variantOrd
            );
        }
    }
}
