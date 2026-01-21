namespace BadWriter.Contracts.Enums
{
    public enum AnchorX { Left, Center, Right, Stretch }
    public enum AnchorY { Top, Center, Bottom, Stretch }
    public enum ScaleMode { None, Stretch, KeepAspectFit, KeepAspectFill }
    public enum ResizableMode { Fixed, Grow, Shrink, Auto }
    public enum OverflowMode { Clip, Wrap, Ellipsis }
    public enum ElementType { Text, Image, TagList, Divider, Custom }
    public enum ContainerContentType { Cards = 1, Containers = 2 }
}