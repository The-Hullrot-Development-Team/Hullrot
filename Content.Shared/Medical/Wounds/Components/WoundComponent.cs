﻿using Content.Shared.FixedPoint;
using Content.Shared.Medical.Wounds.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Medical.Wounds.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(WoundSystem))]
public sealed class WoundComponent : Component
{
    //this is used for caching the parent woundable for use inside and entity query.
    //wounds should NEVER exist without a parent so this will always have a value
    public EntityUid Parent = default;

    //what wound should be created if this wound is healed normally?
    [DataField("scarWound", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string? ScarWound;

    //This is the prototypeId of a wound, this should be populated by the wounding system when the wound is created!
    [ViewVariables] public string prototypeId = string.Empty;

    [DataField("healthDamage")] public FixedPoint2 HealthCapDamage;

    [DataField("integrityDamage")] public FixedPoint2 IntegrityDamage;

    [DataField("severityPercentage")] public FixedPoint2 Severity = 100;

    //How many severity points per woundTick does this part heal passively
    [DataField("baseHealingRate")] public FixedPoint2 BaseHealingRate = 0.05f;

    //How many severity points per woundTick does this part heal ontop of the base rate
    [DataField("healingModifier")] public FixedPoint2 HealingModifier;

    //How much to multiply the Healing modifier
    [DataField("healingMultiplier")] public FixedPoint2 HealingMultiplier = 1.0f;
}
