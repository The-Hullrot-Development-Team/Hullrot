using Content.Shared.Interaction.Events;
using Content.Shared.Toggleable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Temperature;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Network;

namespace Content.Shared.Item.ItemToggle;
/// <summary>
/// Handles generic item toggles, like a welder turning on and off, or an e-sword.
/// </summary>
/// <remarks>
/// If you need extended functionality (e.g. requiring power) then add a new component and use events.
/// </remarks>
public abstract class SharedItemToggleSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly INetManager _netManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemToggleComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ItemToggleHotComponent, IsHotEvent>(OnIsHotEvent);
        SubscribeLocalEvent<ItemToggleComponent, ItemUnwieldedEvent>(TurnOffonUnwielded);
        SubscribeLocalEvent<ItemToggleComponent, ItemWieldedEvent>(TurnOnonWielded);
        SubscribeLocalEvent<ItemToggleComponent, ItemToggleForceToggleEvent>(ForceToggle);
    }
    private void OnUseInHand(EntityUid uid, ItemToggleComponent itemToggle, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (TryComp<WieldableComponent>(uid, out var wieldableComp))
            return;

        Toggle(uid, args.User, itemToggle);
    }

    public void Toggle(EntityUid uid, EntityUid? user = null, ItemToggleComponent? itemToggle = null)
    {
        if (!Resolve(uid, ref itemToggle))
            return;

        if (itemToggle.Activated)
        {
            TryDeactivate(uid, user, itemToggle: itemToggle);
        }
        else
        {
            TryActivate(uid, user, itemToggle: itemToggle);
        }
    }

    public void ForceToggle(EntityUid uid, ItemToggleComponent itemToggle, ref ItemToggleForceToggleEvent args)
    {
        Toggle(uid, args.User, itemToggle);
    }

    public bool TryActivate(EntityUid uid, EntityUid? user = null, ItemToggleComponent? itemToggle = null)
    {
        if (!Resolve(uid, ref itemToggle))
            return false;

        if (itemToggle.Activated)
            return true;

        // The client cannot predict if the attempt to turn on fails or not since the battery and fuel systems are server side (for now). Potential future TODO
        if (_netManager.IsServer)
        {
            var attempt = new ItemToggleActivateAttemptEvent(user);
            RaiseLocalEvent(uid, ref attempt);

            if (attempt.Cancelled)
            {
                //Play the failure to activate noise if there is any.
                _audio.PlayPvs(itemToggle.SoundFailToActivate, uid);
                return false;
            }

            // At this point the server knows that the activation went through successfully, so we play the sounds and make the changes.
            _audio.PlayPvs(itemToggle.SoundActivate, uid);

            // Starts the active sound (like humming).
            if (TryComp(uid, out ItemToggleActiveSoundComponent? activeSound))
            {
                if (activeSound.ActiveSound != null && activeSound.PlayingStream == null)
                {
                    activeSound.PlayingStream = _audio.PlayPvs(activeSound.ActiveSound, uid, AudioParams.Default.WithLoop(true)).Value.Entity;
                }
            }

            Activate(uid, itemToggle);
            var ev = new ItemToggleActivatedEvent();
            RaiseLocalEvent(uid, ref ev);
        }
        var toggleUsed = new ItemToggleDoneEvent(user);
        RaiseLocalEvent(uid, ref toggleUsed);

        return true;
    }

    public bool TryDeactivate(EntityUid uid, EntityUid? user = null, ItemToggleComponent? itemToggle = null)
    {
        if (!Resolve(uid, ref itemToggle))
            return false;

        if (!itemToggle.Activated)
            return true;

        //Since there is currently no system that cancels a deactivation, it's all predicted.
        var attempt = new ItemToggleDeactivateAttemptEvent(user);
        RaiseLocalEvent(uid, ref attempt);

        if (attempt.Cancelled && uid.Id == GetNetEntity(uid).Id)
        {
            return false;
        }
        else
        {
            _audio.PlayPredicted(itemToggle.SoundDeactivate, uid, user);

            //Stops the active sound if there is any.
            if (TryComp(uid, out ItemToggleActiveSoundComponent? activeSound))
            {
                activeSound.PlayingStream = _audio.Stop(activeSound.PlayingStream);
            }

            Deactivate(uid, itemToggle);

            var ev = new ItemToggleDeactivatedEvent();
            RaiseLocalEvent(uid, ref ev);

            var toggleUsed = new ItemToggleDoneEvent(user);
            RaiseLocalEvent(uid, ref toggleUsed);

            return true;
        }
    }

    //Makes the actual changes to the item's components on activation.
    private void Activate(EntityUid uid, ItemToggleComponent itemToggle)
    {
        itemToggle.Activated = true;

        if (TryComp(uid, out ItemToggleSizeComponent? itemToggleSize))
        {
            UpdateItemComponent(uid, itemToggleSize);
        }
        if (TryComp(uid, out ItemToggleMeleeWeaponComponent? itemToggleMelee))
        {
            UpdateWeaponComponent(uid, itemToggleMelee);
        }
        UpdateAppearance(uid, itemToggle);
        UpdateLight(uid, itemToggle);

        Dirty(uid, itemToggle);
    }
    //Makes the actual changes to the item's components on deactivation.
    private void Deactivate(EntityUid uid, ItemToggleComponent itemToggle)
    {
        itemToggle.Activated = false;

        if (TryComp(uid, out ItemToggleSizeComponent? itemToggleSize))
        {
            UpdateItemComponent(uid, itemToggleSize);
        }
        if (TryComp(uid, out ItemToggleMeleeWeaponComponent? itemToggleMelee))
        {
            UpdateWeaponComponent(uid, itemToggleMelee);
        }
        UpdateAppearance(uid, itemToggle);
        UpdateLight(uid, itemToggle);

        Dirty(uid, itemToggle);
    }

    /// <summary>
    /// Used for items that require to be wielded in both hands to activate. For instance the dual energy sword will turn off if not wielded.
    /// </summary>
    private void TurnOffonUnwielded(EntityUid uid, ItemToggleComponent itemToggle, ItemUnwieldedEvent args)
    {
        if (itemToggle.Activated)
            TryDeactivate(uid, args.User, itemToggle: itemToggle);
    }

    /// <summary>
    /// Wieldable items will automatically turn on when wielded.
    /// </summary>
    private void TurnOnonWielded(EntityUid uid, ItemToggleComponent itemToggle, ref ItemWieldedEvent args)
    {
        if (!itemToggle.Activated)
            TryActivate(uid, itemToggle: itemToggle);
    }

    /// <summary>
    /// Used to update item appearance.
    /// </summary>
    private void UpdateAppearance(EntityUid uid, ItemToggleComponent itemToggle)
    {
        if (!TryComp(uid, out AppearanceComponent? appearance))
            return;

        _appearance.SetData(uid, ToggleableLightVisuals.Enabled, itemToggle.Activated, appearance);
        _appearance.SetData(uid, ToggleVisuals.Toggled, itemToggle.Activated, appearance);
    }

    /// <summary>
    /// Used to update light settings.
    /// </summary>
    private void UpdateLight(EntityUid uid, ItemToggleComponent itemToggle)
    {
        if (!_light.TryGetLight(uid, out var light))
            return;

        _light.SetEnabled(uid, itemToggle.Activated, light);
    }

    /// <summary>
    /// Used to update weapon component aspects, like hit sounds, damage values and hidden status while activated.
    /// </summary>
    private void UpdateWeaponComponent(EntityUid uid, ItemToggleMeleeWeaponComponent itemToggleMelee)
    {
        if (!TryComp(uid, out MeleeWeaponComponent? meleeWeapon))
            return;

        //Sets the damage values to the item's default if none is stated.
        itemToggleMelee.ActivatedDamage ??= meleeWeapon.Damage;
        itemToggleMelee.DeactivatedDamage ??= meleeWeapon.Damage;
        //Sets the no damage sound to the item's default if none is stated.
        itemToggleMelee.ActivatedSoundOnHitNoDamage ??= meleeWeapon.NoDamageSound;
        itemToggleMelee.DeactivatedSoundOnHitNoDamage ??= meleeWeapon.NoDamageSound;
        //Sets the swing sound to the item's default if none is stated.
        itemToggleMelee.ActivatedSoundOnSwing ??= meleeWeapon.SwingSound;
        itemToggleMelee.DeactivatedSoundOnSwing ??= meleeWeapon.SwingSound;


        if (IsActivated(uid))
        {
            if (itemToggleMelee.ActivatedDamage != null)
                meleeWeapon.Damage = itemToggleMelee.ActivatedDamage;

            meleeWeapon.HitSound = itemToggleMelee.ActivatedSoundOnHit;

            if (itemToggleMelee.ActivatedSoundOnHitNoDamage != null)
                meleeWeapon.NoDamageSound = itemToggleMelee.ActivatedSoundOnHitNoDamage;

            if (itemToggleMelee.ActivatedSoundOnSwing != null)
                meleeWeapon.SwingSound = itemToggleMelee.ActivatedSoundOnSwing;

            if (itemToggleMelee.DeactivatedSecret)
                meleeWeapon.Hidden = false;
        }
        else
        {
            meleeWeapon.Damage = itemToggleMelee.DeactivatedDamage;

            meleeWeapon.HitSound = itemToggleMelee.DeactivatedSoundOnHit;

            if (itemToggleMelee.DeactivatedSoundOnHitNoDamage != null)
                meleeWeapon.NoDamageSound = itemToggleMelee.DeactivatedSoundOnHitNoDamage;

            if (itemToggleMelee.DeactivatedSoundOnSwing != null)
                meleeWeapon.SwingSound = itemToggleMelee.DeactivatedSoundOnSwing;

            if (itemToggleMelee.DeactivatedSecret)
                meleeWeapon.Hidden = true;
        }

        Dirty(uid, meleeWeapon);
    }

    /// <summary>
    /// Used to update item component aspects, like size values for items that expand when activated (heh).
    /// </summary>
    private void UpdateItemComponent(EntityUid uid, ItemToggleSizeComponent itemToggleSize)
    {
        if (!TryComp(uid, out ItemComponent? item))
            return;

        //Sets the deactivated size to the default if none is stated.
        itemToggleSize.ActivatedSize ??= item.Size;
        itemToggleSize.DeactivatedSize ??= item.Size;

        if (IsActivated(uid))
            _item.SetSize(uid, (ProtoId<ItemSizePrototype>) itemToggleSize.ActivatedSize, item);
        else
            _item.SetSize(uid, (ProtoId<ItemSizePrototype>) itemToggleSize.DeactivatedSize, item);

        Dirty(uid, item);
    }

    public bool IsActivated(EntityUid uid, ItemToggleComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return true; // assume always activated if no component

        return comp.Activated;
    }

    /// <summary>
    /// Used to make the item hot when activated.
    /// </summary>
    private void OnIsHotEvent(EntityUid uid, ItemToggleHotComponent itemToggleHot, IsHotEvent args)
    {
        if (itemToggleHot.IsHotWhenActivated)
            args.IsHot = IsActivated(uid);
    }
}
