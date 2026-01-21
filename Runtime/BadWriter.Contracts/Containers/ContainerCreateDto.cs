namespace BadWriter.Contracts.Containers
{
    public sealed record ContainerCreateDto(
        string WorldId,
        string Name,
        string? Description,
        int? Order
    );
}