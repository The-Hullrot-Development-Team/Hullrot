using Content.Shared.Destructible.Thresholds;
using Content.Shared.Nutrition.Components;
using Content.Shared.Tag;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Nutrition.FoodMetamorphRules;
/// <summary>
/// abstract rules that are used to verify the correct foodSequence for recipe
/// </summary>
[ImplicitDataDefinitionForInheritors]
[Serializable, NetSerializable]
public abstract partial class FoodMetamorphRule
{
    public abstract bool Check(PrototypeManager protoMan, List<FoodSequenceVisualLayer> ingredients);
}

/// <summary>
/// The requirement that the sequence be within the specified size limit
/// </summary>
[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class SequenceLength : FoodMetamorphRule
{
    [DataField(required: true)]
    public MinMax Range = new ();

    public override bool Check(PrototypeManager protoMan, List<FoodSequenceVisualLayer> ingredients)
    {
        return (ingredients.Count <= Range.Max && ingredients.Count >= Range.Min);
    }
}

/// <summary>
/// A requirement that the last element of the sequence have one or all of the required tags
/// </summary>
[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class LastElementHasTags : FoodMetamorphRule
{
    [DataField(required: true)]
    public List<ProtoId<TagPrototype>> Tags = new ();

    [DataField]
    public bool NeedAll = true;

    public override bool Check(PrototypeManager protoMan, List<FoodSequenceVisualLayer> ingredients)
    {
        var lastIngredient = ingredients[ingredients.Count - 1];

        if (!protoMan.TryIndex(lastIngredient.Proto, out var protoIndexed))
            return false;

        foreach (var tag in Tags)
        {
            var containsTag = protoIndexed.Tags.Contains(tag);

            if (NeedAll && !containsTag)
            {
                return false;
            }

            if (!NeedAll && containsTag)
            {
                return true;
            }
        }

        return NeedAll;
    }
}

/// <summary>
/// A requirement that the specified sequence element have one or all of the required tags
/// </summary>
[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class ElementHasTags : FoodMetamorphRule
{
    [DataField(required: true)]
    public int ElementNumber = 0;

    [DataField(required: true)]
    public List<ProtoId<TagPrototype>> Tags = new ();

    [DataField]
    public bool NeedAll = true;

    public override bool Check(PrototypeManager protoMan, List<FoodSequenceVisualLayer> ingredients)
    {
        if (ingredients.Count < ElementNumber + 1)
            return false;

        if (!protoMan.TryIndex(ingredients[ElementNumber].Proto, out var protoIndexed))
            return false;

        foreach (var tag in Tags)
        {
            var containsTag = protoIndexed.Tags.Contains(tag);

            if (NeedAll && !containsTag)
            {
                return false;
            }

            if (!NeedAll && containsTag)
            {
                return true;
            }
        }

        return NeedAll;
    }
}

/// <summary>
/// A requirement that there be X ingredients in the sequence that have one or all of the specified tags.
/// </summary>
[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class IngredientsWithTags : FoodMetamorphRule
{
    [DataField(required: true)]
    public List<ProtoId<TagPrototype>> Tags = new ();

    [DataField(required: true)]
    public MinMax Count = new();

    [DataField]
    public bool NeedAll = true;

    public override bool Check(PrototypeManager protoMan, List<FoodSequenceVisualLayer> ingredients)
    {
        var count = 0;
        foreach (var ingredient in ingredients)
        {
            if (!protoMan.TryIndex(ingredient.Proto, out var protoIndexed))
                continue;

            var allowed = false;
            if (NeedAll)
            {
                allowed = true;
                foreach (var tag in Tags)
                {
                    if (!protoIndexed.Tags.Contains(tag))
                    {
                        allowed = false;
                        break;
                    }
                }
            }
            else
            {
                allowed = false;
                foreach (var tag in Tags)
                {
                    if (protoIndexed.Tags.Contains(tag))
                    {
                        allowed = true;
                        break;
                    }
                }
            }

            if (allowed)
                count++;
        }

        return count >= Count.Min && count <= Count.Max;
    }
}
