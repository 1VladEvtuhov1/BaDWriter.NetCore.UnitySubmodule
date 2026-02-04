using BadWriter.Contracts.Content;

namespace BadWriter.Contracts.Blocks
{
    public sealed record BlockTemplateDto(
        string  Id,
        string  Name,
        BlockDto Payload,
        long    Version,
        long    UpdatedAtUtc,
        bool    IsDeleted
    );
}