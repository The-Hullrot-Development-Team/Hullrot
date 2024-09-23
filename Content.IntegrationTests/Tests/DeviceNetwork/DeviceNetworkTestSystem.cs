using Content.Server.DeviceNetwork.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Reflection;
using DeviceNetworkComponent = Content.Shared.DeviceNetwork.Components.DeviceNetworkComponent;

namespace Content.IntegrationTests.Tests.DeviceNetwork
{
    [Reflect(false)]
    public sealed class DeviceNetworkTestSystem : EntitySystem
    {
        public NetworkPayload LastPayload = default;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<DeviceNetworkComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
        }

        public void SendBaselineTestEvent(EntityUid uid)
        {
            RaiseLocalEvent(uid, new DeviceNetworkPacketEvent(0, "", 0, "", uid, new NetworkPayload()));
        }

        private void OnPacketReceived(EntityUid uid, DeviceNetworkComponent component, DeviceNetworkPacketEvent args)
        {
            LastPayload = args.Data;
        }
    }
}
