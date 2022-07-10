﻿using Content.Shared.Identity.Components;
using Content.Shared.Inventory.Events;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects.Components.Localization;

namespace Content.Shared.Identity;

public partial class IdentitySystem
{
    private void InitializeEvents()
    {
        SubscribeLocalEvent<IdentityComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<IdentityComponent, DidEquipEvent>(OnEquip);
        SubscribeLocalEvent<IdentityComponent, DidUnequipEvent>(OnUnequip);
    }

    // This is where the magic happens
    private void OnInit(EntityUid uid, IdentityComponent component, ComponentInit args)
    {
        component.IdentityEntitySlot = _container.EnsureContainer<ContainerSlot>(uid, SlotName);
        var ident = Spawn(null, Transform(uid).Coordinates);

        // Clone the old entity's grammar to the identity entity, for loc purposes.
        if (TryComp<GrammarComponent>(uid, out var grammar))
        {
            var identityGrammar = EnsureComp<GrammarComponent>(ident);

            foreach (var (k, v) in grammar.Attributes)
            {
                identityGrammar.Attributes.Add(k, v);
            }
        }

        MetaData(ident).EntityName = Name(uid);
        component.IdentityEntitySlot.Insert(ident);
    }

    private void OnEquip(EntityUid uid, IdentityComponent component, DidEquipEvent args)
    {
        UpdateIdentityName(uid, component);
    }

    private void OnUnequip(EntityUid uid, IdentityComponent component, DidUnequipEvent args)
    {
        UpdateIdentityName(uid, component);
    }
}
