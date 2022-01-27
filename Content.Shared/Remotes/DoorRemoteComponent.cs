using Robust.Shared.Analyzers;
using Robust.Shared.GameObjects;

namespace Content.Shared.Remotes
{
    [RegisterComponent]
    [Friend(typeof(DoorRemoteSystem))]
    public class DoorRemoteComponent : Component
    {
        public override string Name => "DoorRemote";

        public OperatingMode Mode = OperatingMode.OpenClose;

        public enum OperatingMode
        {
            OpenClose,
            ToggleBolts
            // ToggleEmergencyAccess
        }
    }
}
