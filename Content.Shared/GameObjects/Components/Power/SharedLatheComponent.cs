﻿using System;
using Robust.Shared.Serialization;

namespace Content.Shared.GameObjects.Components.Power
{
    [Serializable, NetSerializable]
    public enum LatheVisualState : byte
    {
        Idle,
        Producing,
        InsertingMetal,
        InsertingGlass,
        InsertingGold,
        InsertingPhoron
    }
}
