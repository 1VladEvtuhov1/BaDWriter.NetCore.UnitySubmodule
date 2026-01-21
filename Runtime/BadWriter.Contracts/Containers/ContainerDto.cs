namespace BadWriter.Contracts.Containers
{
    public sealed record ContainerDto(
        string Id,
        string Name,
        string Description,
        string? WorldId,
        string? ParentId,
        int Order,
        BadWriter.Contracts.Enums.ContainerContentType ContentType,
        long Version,
        long UpdatedAtUtc,
        bool IsDeleted
    );
}