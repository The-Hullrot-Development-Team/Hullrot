using Content.Shared.Damage;
using Content.Shared.Alert;
using Content.Shared.StatusIcon;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;
using Robust.Shared.Prototypes;

namespace Content.Shared.Nutrition;

[Prototype]
public sealed partial class SatiationPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The base amount at which <see cref="Current"/> decays.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float BaseDecayRate = 0.01666666666f;

    /// <summary>
    /// A dictionary relating SatiationThreshold to the amount of <see cref="Current"/> needed for each one
    /// </summary>
    [DataField(customTypeSerializer: typeof(DictionarySerializer<SatiationThreashold, float>))]
    [ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<SatiationThreashold, float> Thresholds = new()
    {
        { SatiationThreashold.Full, 200.0f },
        { SatiationThreashold.Okay, 150.0f },
        { SatiationThreashold.Concerned, 100.0f },
        { SatiationThreashold.Desperate, 50.0f },
        { SatiationThreashold.Dead, 0.0f }
    };

    /// <summary>
    /// A dictionary relating SatiationThreshold to how much they modify <see cref="BaseDecayRate"/>.
    /// </summary>
    [DataField(customTypeSerializer: typeof(DictionarySerializer<SatiationThreashold, float>))]
    public Dictionary<SatiationThreashold, float> ThresholdDecayModifiers = new()
    {
        { SatiationThreashold.Full, 1.2f },
        { SatiationThreashold.Okay, 1f },
        { SatiationThreashold.Concerned, 0.8f },
        { SatiationThreashold.Desperate, 0.6f },
        { SatiationThreashold.Dead, 0.6f }
    };

    /// <summary>
    /// The amount of slowdown applied when an entity is at SatiationThreashhold.Desperate
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SlowdownModifier = 0.75f;

    /// <summary>
    /// Damage dealt when at given threshold
    /// </summary>
    [DataField(customTypeSerializer: typeof(DictionarySerializer<SatiationThreashold, DamageSpecifier>))]
    public Dictionary<SatiationThreashold, DamageSpecifier> ThresholdDamage = new();

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<AlertCategoryPrototype> AlertCategory = "Hunger";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<SatiationThreashold, ProtoId<AlertPrototype>> Alerts = new()
    {
        { SatiationThreashold.Concerned, "Peckish"},
        { SatiationThreashold.Desperate, "Starving"},
        { SatiationThreashold.Dead, "Starving"}
    };

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<SatiationThreashold, string> Icons = new()
    {
        { SatiationThreashold.Full, "HungerIconOverfed"},
        { SatiationThreashold.Concerned, "HungerIconPeckish"},
        { SatiationThreashold.Desperate, "HungerIconStarving"},
        { SatiationThreashold.Dead, "HungerIconStarving"}
    };
}
