using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Movement.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class JetpackComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("moleUsage")]
    public float MoleUsage = 0.012f;

    [DataField("toggleActionId", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string? ToggleActionId = "ActionToggleJetpack";

    [DataField("toggleAction")] public EntityUid? ToggleAction;

    [ViewVariables(VVAccess.ReadWrite), DataField("acceleration")]
    public float Acceleration = 1f;

    [ViewVariables(VVAccess.ReadWrite), DataField("friction")]
    public float Friction = 0.3f;

    [ViewVariables(VVAccess.ReadWrite), DataField("weightlessModifier")]
    public float WeightlessModifier = 1.2f;
}
