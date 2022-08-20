using Robust.Shared.Map;

namespace Content.Shared.Radiation.Systems;

public abstract class SharedRadiationSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;

    private readonly Direction[] _directions =
    {
        Direction.North, Direction.South, Direction.East, Direction.West,
        //Direction.NorthEast, Direction.NorthWest, Direction.SouthEast, Direction.SouthWest
    };

    public MapId MapId;
    public EntityUid gridUid;
    public Dictionary<Vector2i, float> visitedTiles = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var comp in EntityManager.EntityQuery<RadiationSourceComponent>())
        {
            var ent = comp.Owner;
            var cords = Transform(ent).MapPosition;
            CalculateRadiationMap(cords, comp.RadsPerSecond);
        }
    }

    public void CalculateRadiationMap(MapCoordinates epicenter, float radsPerSecond)
    {
        MapId = epicenter.MapId;

        Vector2i initialTile;
        if (_mapManager.TryFindGridAt(epicenter, out var candidateGrid) &&
            candidateGrid.TryGetTileRef(candidateGrid.WorldToTile(epicenter.Position), out var tileRef) )
        {
            gridUid = tileRef.GridUid;
            initialTile = tileRef.GridIndices;
        }
        else
        {
            return;
        }

        visitedTiles.Clear();
        var visitNext = new Queue<(Vector2i, float)>();
        visitNext.Enqueue((initialTile, radsPerSecond));

        do
        {
            var (current, incomingRads) = visitNext.Dequeue();
            if (visitedTiles.ContainsKey(current))
                continue;

            visitedTiles.Add(current, incomingRads);

            // here is material absorption
            var nextRad = incomingRads;

            // and also remove by distance
            nextRad -= 1f;
            // if no radiation power left - don't propagate further
            if (nextRad <= 0)
                continue;

            foreach (var dir in _directions)
            {
                var next = current.Offset(dir);
                visitNext.Enqueue((next, nextRad));
            }

        } while (visitNext.Count != 0);
    }
}
