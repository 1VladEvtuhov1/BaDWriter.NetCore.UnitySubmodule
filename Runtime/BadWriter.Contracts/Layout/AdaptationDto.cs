namespace BadWriter.Contracts.Layout
{
    public sealed class AdaptationDto
    {
        public Enums.ScaleMode Scale { get; init; } = Enums.ScaleMode.Stretch;
        public bool PreservePixelGrid { get; init; } = true;
        public bool LockAspectRatio { get; init; } = false;
        public Enums.ResizableMode Resizable { get; init; } = Enums.ResizableMode.Auto;
        public ConstraintsDto? Constraints { get; init; }
        public double UiScale { get; init; } = 1.0;
        public double FontScale { get; init; } = 1.0;
    }
}