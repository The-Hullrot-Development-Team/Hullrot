﻿﻿using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.Magic.Components;
using Content.Shared.Mind;

namespace Content.Shared.Magic;

public sealed class SharedSpellbookSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;

    public override void Initialize()
    {
        // TODO: Make Master Spellbook (pointbuy)
        SubscribeLocalEvent<SpellbookComponent, MapInitEvent>(OnInit, before: new []{typeof(SharedMagicSystem)});
        SubscribeLocalEvent<SpellbookComponent, UseInHandEvent>(OnUse);
        SubscribeLocalEvent<SpellbookComponent, SpellbookDoAfterEvent>(OnDoAfter);
    }

    private void OnInit(EntityUid uid, SpellbookComponent component, MapInitEvent args)
    {
        foreach (var (id, charges) in component.SpellActions)
        {
            var spell = _actionContainer.AddAction(uid, id);
            if (spell == null)
                continue;

            // TODO: Just remove the charges from the spellbook?
            // Or just remove this since charges won't matter on the master spellbook
            int? charge = charges;
            if (_actions.GetCharges(spell) != null)
                charge = _actions.GetCharges(spell);

            _actions.SetCharges(spell, charge < 0 ? null : charge);
            component.Spells.Add(spell.Value);
        }
    }

    private void OnUse(EntityUid uid, SpellbookComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        AttemptLearn(uid, component, args);

        args.Handled = true;
    }

    private void OnDoAfter(EntityUid uid, SpellbookComponent component, DoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        if (!component.LearnPermanently)
        {
            _actions.GrantActions(args.Args.User, component.Spells, uid);
            return;
        }

        if (_mind.TryGetMind(args.Args.User, out var mindId, out _))
            _actionContainer.TransferAllActionsWithNewAttached(uid, mindId, args.Args.User);
        else
        {
            foreach (var (id, charges) in component.SpellActions)
            {
                EntityUid? actionId = null;
                if (_actions.AddAction(args.Args.User, ref actionId, id))
                    _actions.SetCharges(actionId, charges < 0 ? null : charges);
            }
        }

        component.SpellActions.Clear();
    }

    private void AttemptLearn(EntityUid uid, SpellbookComponent component, UseInHandEvent args)
    {
        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.LearnTime, new SpellbookDoAfterEvent(), uid, target: uid)
        {
            BreakOnTargetMove = true,
            BreakOnUserMove = true,
            BreakOnDamage = true,
            NeedHand = true //What, are you going to read with your eyes only??
        };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }
}
