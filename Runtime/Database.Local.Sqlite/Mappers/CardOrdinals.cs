using System.Data;

namespace Database.Local.Sqlite.Mappers {

public readonly struct CardOrdinals
{
    public readonly int Id;
    public readonly int ParentId;
    public readonly int Name;
    public readonly int Description;
    public readonly int ArtPath; 
    public readonly int Order;
    public readonly int Version;
    public readonly int UpdatedAtUtc;
    public readonly int IsDeleted;
    public readonly int HasLayout;
    public readonly int LayoutVersion;
    public readonly int LayoutUpdatedAtUtc;
    public readonly int VariantOfId;
    public readonly int VariantOrder;

    public CardOrdinals(IDataRecord r)
    {
        Id                = r.GetOrdinal("id");
        ParentId          = r.GetOrdinal("parent_id");
        Name              = r.GetOrdinal("name");
        Description       = r.GetOrdinal("description");
        ArtPath           = TryGetOrdinal(r, "art_path");
        Order             = TryGetOrdinal(r, "order", "\"order\"");
        Version           = r.GetOrdinal("version");
        UpdatedAtUtc      = r.GetOrdinal("updated_at_utc");
        IsDeleted         = r.GetOrdinal("is_deleted");
        HasLayout         = TryGetOrdinal(r, "has_layout");
        LayoutVersion     = TryGetOrdinal(r, "layout_version");
        LayoutUpdatedAtUtc= TryGetOrdinal(r, "layout_updated_at_utc");
        VariantOfId       = TryGetOrdinal(r, "variant_of_id");
        VariantOrder      = TryGetOrdinal(r, "variant_order");
    }

    private static int TryGetOrdinal(IDataRecord r, params string[] names)
    {
        foreach (var n in names)
        {
            try { return r.GetOrdinal(n); } catch { }
        }
        return -1;
    }
}
}
