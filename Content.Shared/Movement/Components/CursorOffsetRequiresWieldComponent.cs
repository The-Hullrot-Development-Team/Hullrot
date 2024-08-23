using Content.Shared.Wieldable;
using Robust.Shared.GameStates;

namespace Content.Shared.Movement.Components;

/// <summary>
/// Indicates that this item requires wielding for the cursor offset effect to be active.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedWieldableSystem))]
public sealed partial class CursorOffsetRequiresWieldComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan LastPopup;

    [DataField, AutoNetworkedField]
    public TimeSpan PopupCooldown = TimeSpan.FromSeconds(1);
}
