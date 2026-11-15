using Vector2 = System.Numerics.Vector2;

namespace Beasts.Runtime.Features;

internal sealed class MapRenderState
{
    public Vector2[] WorldCircleScreenPoints { get; } = new Vector2[WorldCirclePointsLength];

    // One extra slot: the outline repeats the first point so the polyline is closed.
    public Vector2[] WorldCircleOutlinePoints { get; } = new Vector2[WorldCirclePointsLength + 1];

    // 15 segments looked visibly polygonal once the circle radius goes past ~150 world units.
    public const int WorldCirclePointsLength = 48;
}
