using Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Components;
using Content.Server.Xenoarchaeology.XenoArtifacts.Events;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Mind.Components;
using Content.Shared.Teleportation.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Systems;

public sealed class PortalArtifactSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly LinkedEntitySystem _link = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PortalArtifactComponent, ArtifactActivatedEvent>(OnActivate);
    }

    private void OnActivate(Entity<PortalArtifactComponent> artifact, ref ArtifactActivatedEvent args)
    {
        var map = Transform(artifact).MapID;
        var validMinds = new List<EntityUid>();
        var mindQuery = EntityQueryEnumerator<MindContainerComponent, TransformComponent>();
        while (mindQuery.MoveNext(out var uid, out var mc, out var xform))
        {
            // check if the MindContainer has a Mind and if the entity is not in a container (this also auto excludes AI) and if they are on the same map
            if (mc.HasMind && !_container.IsEntityOrParentInContainer(uid) && xform.MapID == map)
            {
                validMinds.Add(uid);
            }
        }
        //this would only be 0 if there were a station full of AIs and no one else, in that case just stop this function
        if (validMinds.Count == 0)
            return;

        var firstPortal = Spawn(artifact.Comp.PortalProto, _transform.GetMapCoordinates(artifact));

        var target = _random.Pick(validMinds);

        var secondPortal = Spawn(artifact.Comp.PortalProto, _transform.GetMapCoordinates(target));

        //Manual position swapping, because the portal that opens doesn't trigger a collision, and doesn't teleport targets the first time.
        _transform.SwapPositions(target, secondPortal);

        _link.TryLink(firstPortal, secondPortal, true);
    }
}
