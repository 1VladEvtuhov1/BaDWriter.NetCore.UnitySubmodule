using BadWriter.Contracts.Cards;
using BadWriter.Contracts.Containers;
using BadWriter.Contracts.Tags;

namespace BadWriter.Contracts.Worlds
{
    public sealed record WorldManifestDto(
        WorldDto World,
        ContainerDto[] Containers,
        CardDto[] Cards,
        TagDto[]     Tags
    ); 
}

