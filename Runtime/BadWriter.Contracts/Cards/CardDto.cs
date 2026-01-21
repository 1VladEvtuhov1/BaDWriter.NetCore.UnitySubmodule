namespace BadWriter.Contracts.Cards
{
    public sealed record CardDto(
        string Id,
        string Name,
        string ParentId,
        string? ArtPath,
        string Description,
        string[] TagIds,
        long Version,
        long UpdatedAtUtc,
        bool IsDeleted,
        bool HasLayout = false,
        long? LayoutUpdatedAtUtc = null,
        long? LayoutVersion = null,
        string? VariantOfId = null,
        int VariantOrder = 0
    );
}

