namespace BadWriter.Contracts.Layout
{
    public sealed class AnchorDto
    {
        public Enums.AnchorX AnchorX { get; init; } = Enums.AnchorX.Left;
        public Enums.AnchorY AnchorY { get; init; } = Enums.AnchorY.Top;
        public double OffsetX { get; init; } = 0;
        public double OffsetY { get; init; } = 0;
    }
}
