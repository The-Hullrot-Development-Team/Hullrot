using Content.Shared.DeviceNetwork;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.SurveillanceCamera;

[RegisterComponent]
public class SurveillanceCameraRouterComponent : Component
{
    // The name of the subnet connected to this router.
    [DataField("subnetName")]
    public string SubnetName { get; } = default!;

    [ViewVariables]
    // The monitors to route to. This raises an issue related to
    // camera monitors disappearing before sending a D/C packet,
    // this could probably be refreshed every time a new monitor
    // is added or removed from active routing.
    public HashSet<string> MonitorRoutes { get; } = new();

    [ViewVariables]
    // The frequency that talks to this router's subnet.
    public uint SubnetFrequency;
    [DataField("subnetFrequency", customTypeSerializer:typeof(PrototypeIdSerializer<DeviceFrequencyPrototype>))]
    public string SubnetFrequencyId { get; } = default!;
}
