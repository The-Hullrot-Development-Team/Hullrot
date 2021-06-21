﻿using System.Collections.Generic;
using Content.Server.NodeContainer.Nodes;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.Power.Nodes
{
    [DataDefinition]
    public class WireNode : Node
    {
        public override IEnumerable<Node> GetReachableNodes()
        {
            if (!Anchored)
                yield break;

            var compMgr = IoCManager.Resolve<IComponentManager>();
            var grid = IoCManager.Resolve<IMapManager>().GetGrid(Owner.Transform.GridID);
            var gridIndex = grid.TileIndicesFor(Owner.Transform.Coordinates);

            // While we go over adjacent nodes, we build a list of blocked directions due to
            // incoming or outgoing wire terminals.
            var terminalDirs = 0;
            List<(Direction, Node)> nodeDirs = new();

            foreach (var (dir, node) in NodeHelpers.GetCardinalNeighborNodes(compMgr, grid, gridIndex))
            {
                if (node is WireNode && node != this)
                {
                    nodeDirs.Add((dir, node));
                }

                if (node is WireTerminalNode)
                {
                    if (dir == Direction.Invalid)
                    {
                        // On own tile, block direction it faces
                        terminalDirs |= 1 << (int) node.Owner.Transform.LocalRotation.GetCardinalDir();
                    }
                    else
                    {
                        var terminalDir = node.Owner.Transform.LocalRotation.GetCardinalDir();
                        if (terminalDir.GetOpposite() == dir)
                        {
                            // Target tile has a terminal towards us, block the direction.
                            terminalDirs |= 1 << (int) dir;
                            break;
                        }
                    }
                }
            }

            foreach (var (dir, node) in nodeDirs)
            {
                // If there is a wire terminal connecting across this direction, skip the node.
                if (dir != Direction.Invalid && (terminalDirs & (1 << (int) dir)) != 0)
                    continue;

                yield return node;
            }
        }
    }
}
