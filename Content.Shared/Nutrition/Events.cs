﻿using Content.Shared.Chemistry.Components;
using Content.Shared.DoAfter;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Nutrition;

/// <summary>
///     Do after even for food and drink.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ConsumeDoAfterEvent : DoAfterEvent
{
    [DataField("solution", required: true)]
    public string Solution = default!;

    [DataField("flavorMessage", required: true)]
    public string FlavorMessage = default!;

    private ConsumeDoAfterEvent()
    {
    }

    public ConsumeDoAfterEvent(string solution, string flavorMessage)
    {
        Solution = solution;
        FlavorMessage = flavorMessage;
    }

    public override DoAfterEvent Clone() => this;
}

/// <summary>
///     Do after event for vape.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class VapeDoAfterEvent : DoAfterEvent
{
    [DataField("solution", required: true)]
    public Solution Solution = default!;

    [DataField("forced", required: true)]
    public bool Forced = default!;

    private VapeDoAfterEvent()
    {
    }

    public VapeDoAfterEvent(Solution solution, bool forced)
    {
        Solution = solution;
        Forced = forced;
    }

    public override DoAfterEvent Clone() => this;
}

/// <summary>
/// Raised before food is sliced
/// </summary>
[ByRefEvent]
public record struct SliceFoodEvent();

/// <summary>
/// is called after a successful attempt at slicing food.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SliceFoodDoAfterEvent : SimpleDoAfterEvent
{
}

/// <summary>
///    is called when a new ingredient is added to FoodSequence
/// </summary>
public sealed class FoodSequenceIngredientAddedEvent : EntityEventArgs
{
    public EntityUid Start { get; }
    public EntityUid Element { get; }
    public EntityUid? User { get; }
    public ProtoId<FoodSequenceElementPrototype> ElementProto { get; }

    public FoodSequenceIngredientAddedEvent(EntityUid start, EntityUid element, ProtoId<FoodSequenceElementPrototype> proto, EntityUid? user = null)
    {
        Start = start;
        Element = element;
        User = user;
        ElementProto = proto;
    }
}
