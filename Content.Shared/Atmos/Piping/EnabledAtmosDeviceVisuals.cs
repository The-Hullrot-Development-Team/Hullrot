using System;
using Robust.Shared.Serialization;

namespace Content.Shared.Atmos.Piping
{
    [Serializable, NetSerializable]
    public enum OutletInjectorVisuals : byte
    {
        Enabled,
    }

    [Serializable, NetSerializable]
    public enum PassiveVentVisuals : byte
    {
        Enabled,
    }

    [Serializable, NetSerializable]
    public enum VentScrubberVisuals : byte
    {
        Enabled,
    }

    [Serializable, NetSerializable]
    public enum ThermoMachineVisuals : byte
    {
        Enabled,
    }

    [Serializable, NetSerializable]
    public enum PressurePumpVisuals : byte
    {
        Enabled,
    }

    [Serializable, NetSerializable]
    public enum FilterVisuals : byte
    {
        Enabled,
    }
}
