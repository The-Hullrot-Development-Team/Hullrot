using Content.Shared.Charges.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Charges.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedChargesSystem))]
[AutoGenerateComponentState]
public sealed partial class LimitedChargesComponent : Component
{
    /// <summary>
    /// The maximum number of charges
    /// </summary>
    [DataField, ViewVariables]
    [AutoNetworkedField]
    public int MaxCharges = 3;

    /// <summary>
    /// The current number of charges
    /// </summary>
    [DataField, ViewVariables]
    [AutoNetworkedField]
    public int Charges = 3;
}
