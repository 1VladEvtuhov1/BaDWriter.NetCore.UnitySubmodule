using System.Collections.Generic;

namespace BadWriter.Contracts.Content
{
    public sealed class DeviceProfileDto
    {
        public string Id { get; init; } = null!;
        public int MinWidth { get; init; }
        public int? MaxWidth { get; init; }
        public double UiScale { get; init; } = 1.0;
        public double FontScale { get; init; } = 1.0;
        public Dictionary<string, ElementOverrideDto> ElementOverrides { get; init; } = new();
    }
}

