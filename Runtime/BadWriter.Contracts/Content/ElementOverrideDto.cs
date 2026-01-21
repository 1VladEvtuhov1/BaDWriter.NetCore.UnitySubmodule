using System.Collections.Generic;
using BadWriter.Contracts.Layout;

namespace BadWriter.Contracts.Content
{
    public sealed class ElementOverrideDto
    {
        public NormalizedFrameDto? Frame { get; init; }
        public AnchorDto? Anchors { get; init; }
        public AdaptationDto? Adaptation { get; init; }
        public double? AspectRatio { get; init; }
        public bool? Hidden { get; init; }
        public Dictionary<string, object>? Props { get; init; }
    }
}

