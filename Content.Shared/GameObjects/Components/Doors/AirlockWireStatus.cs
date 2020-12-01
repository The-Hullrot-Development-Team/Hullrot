﻿using System;
using Robust.Shared.Serialization;

namespace Content.Shared.GameObjects.Components.Doors
{
    [Serializable, NetSerializable]
    public enum AirlockWireStatus : byte
    {
        PowerIndicator,
        BoltIndicator,
        BoltLightIndicator,
        AIControlIndicator,
        TimingIndicator,
        SafetyIndicator,
    }
}
