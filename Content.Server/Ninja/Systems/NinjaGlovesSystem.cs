using Content.Server.Communications;
using Content.Server.DoAfter;
using Content.Server.Ninja.Systems;
using Content.Server.Power.Components;
using Content.Shared.Communications;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Toggleable;

namespace Content.Server.Ninja.Systems;

/// <summary>
/// Handles the toggle gloves action.
/// </summary>
public sealed class NinjaGlovesSystem : SharedNinjaGlovesSystem
{
    [Dependency] private readonly EmagProviderSystem _emagProvider = default!;
    [Dependency] private readonly SharedBatteryDrainerSystem _drainer = default!;
    [Dependency] private readonly SharedStunProviderSystem _stunProvider = default!;
    [Dependency] private readonly SpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly CommsHackerSystem _commsHacker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaGlovesComponent, ToggleActionEvent>(OnToggleAction);
    }

    /// <summary>
    /// Toggle gloves, if the user is a ninja wearing a ninja suit.
    /// </summary>
    private void OnToggleAction(EntityUid uid, NinjaGlovesComponent comp, ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var user = args.Performer;
        // need to wear suit to enable gloves
        if (!TryComp<SpaceNinjaComponent>(user, out var ninja)
            || ninja.Suit == null
            || !HasComp<NinjaSuitComponent>(ninja.Suit.Value))
        {
            Popup.PopupEntity(Loc.GetString("ninja-gloves-not-wearing-suit"), user, user);
            return;
        }

        // show its state to the user
        var enabling = comp.User == null;
        Appearance.SetData(uid, ToggleVisuals.Toggled, enabling);
        var message = Loc.GetString(enabling ? "ninja-gloves-on" : "ninja-gloves-off");
        Popup.PopupEntity(message, user, user);

        if (enabling)
        {
            EnableGloves(uid, comp, user, ninja);
        }
        else
        {
            DisableGloves(uid, comp);
        }
    }

    private void EnableGloves(EntityUid uid, NinjaGlovesComponent comp, EntityUid user, SpaceNinjaComponent ninja)
    {
        comp.User = user;
        Dirty(comp);
        _ninja.AssignGloves(ninja, uid);

        var drainer = EnsureComp<BatteryDrainerComponent>(user);
        var stun = EnsureComp<StunProviderComponent>(user);
        if (_ninja.GetNinjaBattery(user, out var battery, out var _))
        {
            _drainer.SetBattery(drainer, battery);
            _stunProvider.SetBattery(stun, battery);
        }

        var emag = EnsureComp<EmagProviderComponent>(user);
        _emagProvider.SetWhitelist(user, comp.DoorjackWhitelist, emag);

        EnsureComp<ResearchStealerComponent>(user);
        // prevent calling in multiple threats by toggling gloves after
        if (_mind.TryGetRole<NinjaRole>(user) && !role.CalledInThreat)
        {
            var hacker = EnsureComp<CommsHackerComponent>(user);
            _commsHacker.SetThreats(user, _ninja.RuleConfig().Threats, hacker);
        }
    }
}
