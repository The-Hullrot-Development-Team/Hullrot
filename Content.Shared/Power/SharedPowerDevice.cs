#nullable enable
using System;
using Robust.Shared.Serialization;

namespace Content.Shared.Power
{
    [Serializable, NetSerializable]
    public enum PowerDeviceVisuals
    {
        VisualState,
        Powered
    }
}
