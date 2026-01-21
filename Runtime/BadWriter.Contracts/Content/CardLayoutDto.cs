using System.Collections.Generic;

namespace BadWriter.Contracts.Content
{
    public sealed record CardLayoutDto(
        string                       CardId,
        long                         LayoutVersion,
        long                         UpdatedAtUtc,
        bool                         IsDeleted,
        List<BlockDto>               Blocks,
        List<DeviceProfileDto>?      Profiles = null
    );
}

