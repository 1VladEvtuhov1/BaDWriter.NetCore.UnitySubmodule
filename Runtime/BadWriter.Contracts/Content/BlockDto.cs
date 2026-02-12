using System.Collections.Generic;

namespace BadWriter.Contracts.Content
{
    public sealed class BlockDto
    {
        public string Id { get; init; } = null!;
        public List<ElementDto> Children { get; init; } = new();
        public double? DesignWidthPx { get; init; } // optional: authoring width in pixels
        public double? DesignHeightPx { get; init; } // optional: authoring height in pixels
        public double? DesignAspectRatio { get; init; } // optional: helps fit whole block
        public double PaddingPx { get; init; } // applied in device space
    }
}