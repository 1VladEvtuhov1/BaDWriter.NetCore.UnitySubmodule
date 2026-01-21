namespace BadWriter.Contracts.Tags
{
    public sealed record TagDto(
        string Id,
        string Name,
        int ColorArgb,
        long Version      = 0,
        long UpdatedAtUtc = 0,
        bool IsDeleted    = false
    ); 
}
