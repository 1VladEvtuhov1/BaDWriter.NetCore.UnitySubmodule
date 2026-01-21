using System.Collections.Generic;
using BadWriter.Contracts.Enums;
using BadWriter.Contracts.Layout;

namespace BadWriter.Contracts.Content
{
    public sealed class ElementDto
    {
        public string Id { get; init; } = null!;
        public ElementType Type { get; init; }

        public NormalizedFrameDto Frame { get; init; } = null!;
        public AnchorDto Anchors { get; init; } = new();
        public AdaptationDto Adaptation { get; init; } = new();

        public double? AspectRatio { get; init; } // for images/video; W/H from design
        public Enums.OverflowMode Overflow { get; init; } = Enums.OverflowMode.Wrap;

        /// <summary> Layer order; higher renders above. </summary>
        public int ZIndex { get; init; } = 0;

        /// <summary> Arbitrary content/style payload. </summary>
        public Dictionary<string, object>? Props { get; init; }
    }
}