﻿using System.Collections.Generic;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.Power.Nodes;
using Content.Server.Wires.Components;
using Content.Shared.Wires;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.Wires.EntitySystems
{
    [UsedImplicitly]
    public sealed class WireVisSystem : EntitySystem
    {
        [Dependency] private readonly IMapManager _mapManager = default!;

        private readonly HashSet<EntityUid> _toUpdate = new();

        public void QueueUpdate(EntityUid uid)
        {
            _toUpdate.Add(uid);
        }

        public override void Initialize()
        {
            base.Initialize();

            UpdatesAfter.Add(typeof(NodeGroupSystem));
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach (var uid in _toUpdate)
            {
                if (!ComponentManager.TryGetComponent(uid, out NodeContainerComponent? nodeContainer)
                    || !ComponentManager.TryGetComponent(uid, out WireVisComponent? wireVis)
                    || !ComponentManager.TryGetComponent(uid, out AppearanceComponent? appearance))
                {
                    continue;
                }

                if (wireVis.Node == null)
                    continue;

                var mask = WireVisDirFlags.None;

                var transform = ComponentManager.GetComponent<ITransformComponent>(uid);
                var grid = _mapManager.GetGrid(transform.GridID);
                var tile = grid.TileIndicesFor(transform.Coordinates);
                var node = nodeContainer.GetNode<WireNode>(wireVis.Node);

                foreach (var reachable in node.ReachableNodes)
                {
                    if (reachable is not WireNode)
                        continue;

                    var otherTransform = reachable.Owner.Transform;
                    if (otherTransform.GridID != grid.Index)
                        continue;

                    var otherTile = grid.TileIndicesFor(otherTransform.Coordinates);
                    var diff = otherTile - tile;

                    mask |= diff switch
                    {
                        (0, 1) => WireVisDirFlags.North,
                        (0, -1) => WireVisDirFlags.South,
                        (1, 0) => WireVisDirFlags.East,
                        (-1, 0) => WireVisDirFlags.West,
                        _ => WireVisDirFlags.None
                    };
                }

                appearance.SetData(WireVisVisuals.ConnectedMask, mask);
            }

            _toUpdate.Clear();
        }
    }
}
