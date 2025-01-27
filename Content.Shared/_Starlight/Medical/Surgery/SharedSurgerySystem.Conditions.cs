﻿using Content.Shared.Body.Part;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Humanoid;
using System.Linq;
using Content.Shared.Starlight.Medical.Surgery.Steps.Parts;
using Content.Shared.Starlight.Medical.Surgery.Events;
using Content.Shared.Starlight.Medical.Surgery.Effects.Step;

namespace Content.Shared.Starlight.Medical.Surgery;
// Based on the RMC14.
// https://github.com/RMC-14/RMC-14
public abstract partial class SharedSurgerySystem
{
    protected List<Type> _accents = [];
    private void InitializeConditions()
    {
        _accents = _reflectionManager.FindTypesWithAttribute<RegisterComponentAttribute>()
            .Where(type => type.Name.EndsWith("AccentComponent"))
            .ToList();

        SubscribeLocalEvent<SurgeryPartConditionComponent, SurgeryValidEvent>(OnPartConditionValid);
        SubscribeLocalEvent<SurgerySpeciesConditionComponent, SurgeryValidEvent>(OnSpeciesConditionValid);
        SubscribeLocalEvent<SurgeryOrganExistConditionComponent, SurgeryValidEvent>(OnOrganExistConditionValid);
        SubscribeLocalEvent<SurgeryOrganDontExistConditionComponent, SurgeryValidEvent>(OnOrganDontExistConditionValid);
        SubscribeLocalEvent<SurgeryAnyAccentConditionComponent, SurgeryValidEvent>(OnAnyAccentConditionValid);
        SubscribeLocalEvent<SurgeryAnyLimbSlotConditionComponent, SurgeryValidEvent>(OnAnyLimbSlotConditionValid);
    }
    private void OnOrganDontExistConditionValid(Entity<SurgeryOrganDontExistConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (ent.Comp.Organ?.Count != 1) return;
        var type = ent.Comp.Organ.Values.First().Component.GetType();

        var organs = _body.GetPartOrgans(args.Part, Comp<BodyPartComponent>(args.Part));
        foreach (var organ in organs)
            if (HasComp(organ.Id, type))
            {
                args.Cancelled = true;
                return;
            }
    }
    private void OnOrganExistConditionValid(Entity<SurgeryOrganExistConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (ent.Comp.Organ?.Count != 1) return;
        
        if (TryComp<BodyPartComponent>(args.Body, out var itemPart))
        {
            var organs = _body.GetPartOrgans(args.Body, itemPart);
            var type = ent.Comp.Organ.Values.First().Component.GetType();
            foreach (var organ in organs)
                if (HasComp(organ.Id, type))
                    return;
            args.Cancelled = true;
            Logger.Warning("don't have organs at part");
        }
        else
        {
            var organs = _body.GetPartOrgans(args.Part, Comp<BodyPartComponent>(args.Part));
            var type = ent.Comp.Organ.Values.First().Component.GetType();
            foreach (var organ in organs)
                if (HasComp(organ.Id, type))
                    return;
            args.Cancelled = true;
        }
    }

    private void OnPartConditionValid(Entity<SurgeryPartConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (ent.Comp.Parts.Count == 0)
            return;
        
        if (TryComp<BodyPartComponent>(args.Body, out var itemPart) && itemPart.PartType is BodyPartType item && !ent.Comp.Parts.Contains(item))
        {
            Logger.Warning("don't have part at part");
            args.Cancelled = true;
        }

        if (CompOrNull<BodyPartComponent>(args.Part)?.PartType is BodyPartType part && !ent.Comp.Parts.Contains(part))
            args.Cancelled = true;
    }
    private void OnSpeciesConditionValid(Entity<SurgerySpeciesConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!EntityManager.TryGetComponent<HumanoidAppearanceComponent>(args.Body, out var humanoidAppearanceComponent))
        {
            args.Cancelled = true;
            return;
        }

        if (ent.Comp.SpeciesBlacklist.Contains(humanoidAppearanceComponent.Species))
        {
            args.Cancelled = true;
            return;
        }

        if (ent.Comp.SpeciesWhitelist.Count > 0 && !ent.Comp.SpeciesWhitelist.Contains(humanoidAppearanceComponent.Species))
        {
            args.Cancelled = true;
            return;
        }
    }
    private void OnAnyAccentConditionValid(Entity<SurgeryAnyAccentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        foreach (var accent in _accents)
            if (HasComp(args.Body, accent))
                return;
        args.Cancelled = true;
    }
    private void OnAnyLimbSlotConditionValid(Entity<SurgeryAnyLimbSlotConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (CompOrNull<BodyPartComponent>(args.Part) is not BodyPartComponent bodyPartComponent)
            return;

        if (_body.TryGetFreePartSlot(args.Part, out var slotId, bodyPartComponent))
            args.Suffix = slotId;
        else
            args.Cancelled = true;
    }
}
