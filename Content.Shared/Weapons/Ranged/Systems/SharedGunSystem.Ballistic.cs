using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;


    protected virtual void InitializeBallistic()
    {
        SubscribeLocalEvent<BallisticAmmoProviderComponent, ComponentInit>(OnBallisticInit);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, MapInitEvent>(OnBallisticMapInit);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, TakeAmmoEvent>(OnBallisticTakeAmmo);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, GetAmmoCountEvent>(OnBallisticAmmoCount);

        SubscribeLocalEvent<BallisticAmmoProviderComponent, ExaminedEvent>(OnBallisticExamine);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, GetVerbsEvent<Verb>>(OnBallisticVerb);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, InteractUsingEvent>(OnBallisticInteractUsing);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, AfterInteractEvent>(OnBallisticAfterInteract);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, AmmoFillDoAfterEvent>(OnBallisticAmmoFillDoAfter);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, UseInHandEvent>(OnBallisticUse);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, ActivateInWorldEvent>(OnBallisticActivate);
    }

    private void OnBallisticUse(EntityUid uid, BallisticAmmoProviderComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (component.Cycleable)
            ManualCycle(uid, component, TransformSystem.GetMapCoordinates(uid), args.User);
        else
            ToggleBolt(uid, component, args.User);

        args.Handled = true;
    }

    private void OnBallisticActivate(EntityUid uid, BallisticAmmoProviderComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        ToggleBolt(uid, component, args.User);
    }

    private void OnBallisticInteractUsing(EntityUid uid, BallisticAmmoProviderComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_whitelistSystem.IsWhitelistFailOrNull(component.Whitelist, args.Used))
            return;

        if (GetBallisticShots(component) >= component.Capacity)
            return;

        if (component.BoltClosed != null && component.BoltClosed != component.BoltClosedReload)
            return;

        component.Entities.Add(args.Used);
        Containers.Insert(args.Used, component.Container);
        // Not predicted so
        Audio.PlayPredicted(component.SoundInsert, uid, args.User);
        args.Handled = true;
        UpdateBallisticAppearance(uid, component);
        Dirty(uid, component);
    }

    private void OnBallisticAfterInteract(EntityUid uid, BallisticAmmoProviderComponent component, AfterInteractEvent args)
    {
        if (args.Handled ||
            !component.MayTransfer ||
            !Timing.IsFirstTimePredicted ||
            args.Target == null ||
            args.Used == args.Target ||
            Deleted(args.Target) ||
            !TryComp<BallisticAmmoProviderComponent>(args.Target, out var targetComponent) ||
            targetComponent.Whitelist == null)
        {
            return;
        }

        args.Handled = true;

        // Continuous loading
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, component.FillDelay, new AmmoFillDoAfterEvent(), used: uid, target: args.Target, eventTarget: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
            NeedHand = true,
        });
    }

    private void OnBallisticAmmoFillDoAfter(EntityUid uid, BallisticAmmoProviderComponent component, AmmoFillDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (Deleted(args.Target) ||
            !TryComp<BallisticAmmoProviderComponent>(args.Target, out var target) ||
            target.Whitelist == null)
            return;

        if (target.Entities.Count + target.UnspawnedCount == target.Capacity)
        {
            Popup(
                Loc.GetString("gun-ballistic-transfer-target-full",
                    ("entity", args.Target)),
                args.Target,
                args.User);
            return;
        }

        if (component.Entities.Count + component.UnspawnedCount == 0)
        {
            Popup(
                Loc.GetString("gun-ballistic-transfer-empty",
                    ("entity", uid)),
                uid,
                args.User);
            return;
        }

        if (target.BoltClosed != null && target.BoltClosed != target.BoltClosedReload)
        {
            return;
        }

        void SimulateInsertAmmo(EntityUid ammo, EntityUid ammoProvider, EntityCoordinates coordinates)
        {
            var evInsert = new InteractUsingEvent(args.User, ammo, ammoProvider, coordinates);
            RaiseLocalEvent(ammoProvider, evInsert);
        }

        List<(EntityUid? Entity, IShootable Shootable)> ammo = new();
        var evTakeAmmo = new TakeAmmoEvent(1, ammo, Transform(uid).Coordinates, args.User);
        RaiseLocalEvent(uid, evTakeAmmo);

        foreach (var (ent, _) in ammo)
        {
            if (ent == null)
                continue;

            if (_whitelistSystem.IsWhitelistFail(target.Whitelist, ent.Value))
            {
                Popup(
                    Loc.GetString("gun-ballistic-transfer-invalid",
                        ("ammoEntity", ent.Value),
                        ("targetEntity", args.Target.Value)),
                    uid,
                    args.User);

                SimulateInsertAmmo(ent.Value, uid, Transform(uid).Coordinates);
            }
            else
            {
                // play sound to be cool
                Audio.PlayPredicted(component.SoundInsert, uid, args.User);
                SimulateInsertAmmo(ent.Value, args.Target.Value, Transform(args.Target.Value).Coordinates);
            }

            if (IsClientSide(ent.Value))
                Del(ent.Value);
        }

        // repeat if there is more space in the target and more ammo to fill it
        var moreSpace = target.Entities.Count + target.UnspawnedCount < target.Capacity;
        var moreAmmo = component.Entities.Count + component.UnspawnedCount > 0;
        args.Repeat = moreSpace && moreAmmo;
    }

    private void OnBallisticVerb(EntityUid uid, BallisticAmmoProviderComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        if (component.Cycleable)
        {
            args.Verbs.Add(new Verb()
            {
                Text = Loc.GetString("gun-ballistic-cycle"),
                Disabled = GetBallisticShots(component) == 0,
                Act = () => ManualCycle(uid, component, TransformSystem.GetMapCoordinates(uid), args.User),
            });

        }

        if (component.BoltClosed != null)
        {
            args.Verbs.Add(new Verb()
            {
                Text = component.BoltClosed.Value ? Loc.GetString("gun-chamber-bolt-open") : Loc.GetString("gun-chamber-bolt-close"),
                Act = () =>
                {
                    ToggleBolt(uid, component, args.User);
                }
            });

        }
    }

    private void OnBallisticExamine(EntityUid uid, BallisticAmmoProviderComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        string boltState;

        using (args.PushGroup(nameof(BallisticAmmoProviderComponent)))
        {
            if (component.BoltClosed != null)
            {
                if (component.BoltClosed == true)
                    boltState = Loc.GetString("gun-chamber-bolt-open-state");
                else
                    boltState = Loc.GetString("gun-chamber-bolt-closed-state");
                args.PushMarkup(Loc.GetString("gun-chamber-bolt", ("bolt", boltState),
                    ("color", component.BoltClosed.Value ? Color.FromHex("#94e1f2") : Color.FromHex("#f29d94"))));
            }

            args.PushMarkup(Loc.GetString("gun-magazine-examine", ("color", AmmoExamineColor), ("count", GetBallisticShots(component))));
        }
    }

    private void ManualCycle(EntityUid uid, BallisticAmmoProviderComponent component, MapCoordinates coordinates, EntityUid? user = null, GunComponent? gunComp = null)
    {
        if (!component.Cycleable)
            return;

        // Reset shotting for cycling
        if (Resolve(uid, ref gunComp, false) &&
            gunComp is { FireRateModified: > 0f } &&
            !Paused(uid))
        {
            gunComp.NextFire = Timing.CurTime + TimeSpan.FromSeconds(1 / gunComp.FireRateModified);
            Dirty(uid, gunComp);
        }

        Dirty(uid, component);
        Audio.PlayPredicted(component.SoundRack, uid, user);

        var shots = GetBallisticShots(component);
        Cycle(uid, component, coordinates);

        var text = Loc.GetString(shots == 0 ? "gun-ballistic-cycled-empty" : "gun-ballistic-cycled");

        Popup(text, uid, user);
        UpdateBallisticAppearance(uid, component);
        UpdateAmmoCount(uid);
    }

    protected abstract void Cycle(EntityUid uid, BallisticAmmoProviderComponent component, MapCoordinates coordinates);

    /// <summary>
    /// Sets the bolt's positional value to the other state
    /// </summary>
    public void ToggleBolt(EntityUid uid, BallisticAmmoProviderComponent component, EntityUid? user = null)
    {
        if (component.BoltClosed == null)
            return;

        SetBoltClosed(uid, component, !component.BoltClosed.Value, user);
    }

    private void OnBallisticInit(EntityUid uid, BallisticAmmoProviderComponent component, ComponentInit args)
    {
        component.Container = Containers.EnsureContainer<Container>(uid, "ballistic-ammo");
        // TODO: This is called twice though we need to support loading appearance data (and we need to call it on MapInit
        // to ensure it's correct).
        UpdateBallisticAppearance(uid, component);
    }

    private void OnBallisticMapInit(EntityUid uid, BallisticAmmoProviderComponent component, MapInitEvent args)
    {
        // TODO this should be part of the prototype, not set on map init.
        // Alternatively, just track spawned count, instead of unspawned count.
        if (component.Proto != null)
        {
            component.UnspawnedCount = Math.Max(0, component.Capacity - component.Container.ContainedEntities.Count);
            UpdateBallisticAppearance(uid, component);
            Dirty(uid, component);
        }
    }

    protected int GetBallisticShots(BallisticAmmoProviderComponent component)
    {
        return component.Entities.Count + component.UnspawnedCount;
    }

    private void OnBallisticTakeAmmo(EntityUid uid, BallisticAmmoProviderComponent component, TakeAmmoEvent args)
    {
        if (component.BoltClosed == false)
        {
            args.Reason = Loc.GetString("gun-chamber-bolt-ammo");
            return;
        }

        for (var i = 0; i < args.Shots; i++)
        {
            EntityUid entity;

            if (component.Entities.Count > 0)
            {
                entity = component.Entities[^1];

                args.Ammo.Add((entity, EnsureShootable(entity)));

                if (component.AutoCycle)
                {
                    component.Entities.RemoveAt(component.Entities.Count - 1);
                    Containers.Remove(entity, component.Container);
                }
            }
            else if (component.UnspawnedCount > 0)
            {
                entity = Spawn(component.Proto, args.Coordinates);
                component.UnspawnedCount--;

                args.Ammo.Add((entity, EnsureShootable(entity)));

                if (!component.AutoCycle)
                {
                    component.Entities.Add(entity);
                    Containers.Insert(entity, component.Container);
                }
            }
        }

        UpdateBallisticAppearance(uid, component);
        Dirty(uid, component);
    }

    private void OnBallisticAmmoCount(EntityUid uid, BallisticAmmoProviderComponent component, ref GetAmmoCountEvent args)
    {
        args.Count = GetBallisticShots(component);
        args.Capacity = component.Capacity;
    }

    public void UpdateBallisticAppearance(EntityUid uid, BallisticAmmoProviderComponent component)
    {
        if (!Timing.IsFirstTimePredicted || !TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        Appearance.SetData(uid, AmmoVisuals.AmmoCount, GetBallisticShots(component), appearance);
        Appearance.SetData(uid, AmmoVisuals.AmmoMax, component.Capacity, appearance);
    }

    public void SetBallisticUnspawned(Entity<BallisticAmmoProviderComponent> entity, int count)
    {
        if (entity.Comp.UnspawnedCount == count)
            return;

        entity.Comp.UnspawnedCount = count;
        UpdateBallisticAppearance(entity.Owner, entity.Comp);
        UpdateAmmoCount(entity.Owner);
        Dirty(entity);
    }

    /// <summary>
    /// Updates the bolt to its new state
    /// </summary>
    public void SetBoltClosed(EntityUid uid, BallisticAmmoProviderComponent component, bool value, EntityUid? user = null, AppearanceComponent? appearance = null)
    {
        if (component.BoltClosed == null || value == component.BoltClosed)
            return;

        Appearance.SetData(uid, AmmoVisuals.BoltClosed, value, appearance);

        if (value)
        {
            if (user != null)
                PopupSystem.PopupClient(Loc.GetString("gun-chamber-bolt-closed"), uid, user.Value);

            Audio.PlayPredicted(component.BoltClosedSound, uid, user);
        }
        else
        {
            Cycle(uid, component, TransformSystem.GetMapCoordinates(uid));

            UpdateAmmoCount(uid);

            if (user != null)
                PopupSystem.PopupClient(Loc.GetString("gun-chamber-bolt-opened"), uid, user.Value);

            Audio.PlayPredicted(component.BoltOpenedSound, uid, user);
            UpdateBallisticAppearance(uid, component);
        }

        component.BoltClosed = value;
        Dirty(uid, component);
    }
}

/// <summary>
/// DoAfter event for filling one ballistic ammo provider from another.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class AmmoFillDoAfterEvent : SimpleDoAfterEvent
{
}
