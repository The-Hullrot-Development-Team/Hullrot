﻿using Content.Shared.Tabletop.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Players;
using Robust.Shared.ViewVariables;

namespace Content.Server.Tabletop.Components
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedTabletopDraggableComponent))]
    public class TabletopDraggableComponent : SharedTabletopDraggableComponent
    {
        private NetUserId? _draggingPlayer;

        // The player dragging the piece
        [ViewVariables]
        public NetUserId? DraggingPlayer
        {
            get => _draggingPlayer;
            set
            {
                _draggingPlayer = value;
                Dirty();
            }
        }

        public override ComponentState GetComponentState(ICommonSession player)
        {
            return new TabletopDraggableComponentState(DraggingPlayer);
        }
    }
}
