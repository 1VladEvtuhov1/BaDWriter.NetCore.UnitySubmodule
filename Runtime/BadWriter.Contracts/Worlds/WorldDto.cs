namespace BadWriter.Contracts.Worlds
{
    public sealed record WorldDto(
        string Id,
        string Name,
        string Description,
        long   Version,
        long   UpdatedAtUtc,
        bool   IsDeleted
    );
}


