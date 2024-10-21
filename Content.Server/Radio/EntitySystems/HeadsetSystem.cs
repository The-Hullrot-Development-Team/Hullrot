using System.Collections.Frozen;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Emp;
using Content.Server.Interaction;
using Content.Server.Radio.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Radio.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Radio.EntitySystems;

public sealed class HeadsetSystem : SharedHeadsetSystem
{
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private FrozenDictionary<string, RadioChannelPrototype> _channels = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HeadsetComponent, RadioReceiveEvent>(OnHeadsetReceive);
        SubscribeLocalEvent<HeadsetComponent, EncryptionChannelsChangedEvent>(OnKeysChanged);

        SubscribeLocalEvent<WearingHeadsetComponent, EntitySpokeEvent>(OnSpeak);

        SubscribeLocalEvent<HeadsetComponent, EmpPulseEvent>(OnEmpPulse);

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypeReload);

        SubscribeLocalEvent<HeadsetComponent, GetVerbsEvent<Verb>>(OnGetVerbs);

        CacheChannels();
    }

    private void OnKeysChanged(EntityUid uid, HeadsetComponent component, EncryptionChannelsChangedEvent args)
    {
        UpdateRadioChannels(uid, component, args.Component);
    }

    private void UpdateRadioChannels(EntityUid uid, HeadsetComponent headset, EncryptionKeyHolderComponent? keyHolder = null)
    {
        // make sure to not add ActiveRadioComponent when headset is being deleted
        if (!headset.Enabled || MetaData(uid).EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        if (!Resolve(uid, ref keyHolder))
            return;

        if (keyHolder.Channels.Count == 0)
            RemComp<ActiveRadioComponent>(uid);
        else
            EnsureComp<ActiveRadioComponent>(uid).Channels = new(keyHolder.Channels);
    }

    private void OnSpeak(EntityUid uid, WearingHeadsetComponent component, EntitySpokeEvent args)
    {
        if (args.Channel != null
            && TryComp(component.Headset, out EncryptionKeyHolderComponent? keys)
            && keys.Channels.Contains(args.Channel.ID))
        {
            _radio.SendRadioMessage(uid, args.Message, args.Channel, component.Headset);
            args.Channel = null; // prevent duplicate messages from other listeners.
        }
    }

    protected override void OnGotEquipped(EntityUid uid, HeadsetComponent component, GotEquippedEvent args)
    {
        base.OnGotEquipped(uid, component, args);
        if (component.IsEquipped && component.Enabled)
        {
            EnsureComp<WearingHeadsetComponent>(args.Equipee).Headset = uid;
            UpdateRadioChannels(uid, component);
        }
    }

    protected override void OnGotUnequipped(EntityUid uid, HeadsetComponent component, GotUnequippedEvent args)
    {
        base.OnGotUnequipped(uid, component, args);
        component.IsEquipped = false;
        RemComp<ActiveRadioComponent>(uid);
        RemComp<WearingHeadsetComponent>(args.Equipee);
    }

    public void SetEnabled(EntityUid uid, bool value, HeadsetComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.Enabled == value)
            return;

        if (!value)
        {
            RemCompDeferred<ActiveRadioComponent>(uid);

            if (component.IsEquipped)
                RemCompDeferred<WearingHeadsetComponent>(Transform(uid).ParentUid);
        }
        else if (component.IsEquipped)
        {
            EnsureComp<WearingHeadsetComponent>(Transform(uid).ParentUid).Headset = uid;
            UpdateRadioChannels(uid, component);
        }
    }

    private void OnHeadsetReceive(EntityUid uid, HeadsetComponent component, ref RadioReceiveEvent args)
    {
        if (!TryComp(Transform(uid).ParentUid, out ActorComponent? actor))
            return;

        _netMan.ServerSendMessage(args.ChatMsg, actor.PlayerSession.Channel);

        if (uid == args.RadioSource)
            return;

        if (!component.SoundChannels.Contains(args.Channel.ID))
            return;

        _audio.PlayEntity(component.Sound, actor.PlayerSession, uid);
    }

    private void OnEmpPulse(EntityUid uid, HeadsetComponent component, ref EmpPulseEvent args)
    {
        if (component.Enabled)
        {
            args.Affected = true;
            args.Disabled = true;
        }
    }

    private void OnPrototypeReload(PrototypesReloadedEventArgs args)
    {
        CacheChannels();
    }

    private void OnGetVerbs(EntityUid uid, HeadsetComponent component, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || args.Hands == null)
            return;

        if (!_interaction.InRangeUnobstructed(args.User, args.Target))
            return;

        if (!TryComp(uid, out EncryptionKeyHolderComponent? keyHolder))
            return;

        foreach ((var channel, var index) in keyHolder.Channels.Select(static (channel, index) => (channel, index)))
        {
            var name = _channels[channel].LocalizedName;

            var toggled = component.SoundChannels.Contains(channel);

            args.Verbs.Add(new()
            {
                Text = toggled ? $"[bold]{name}" : name,
                Priority = index,
                Category = VerbCategory.ToggleHeadsetSound,
                Act = () => ToggleHeadsetSound((uid, component), channel, !toggled)
            });
        }
    }

    private void CacheChannels()
    {
        _channels = _prototype.EnumeratePrototypes<RadioChannelPrototype>().ToFrozenDictionary(prototype => prototype.ID);
    }

    public static void ToggleHeadsetSound(Entity<HeadsetComponent> headset, string channel, bool on)
    {
        if (on)
            headset.Comp.SoundChannels.Add(channel);
        else
            headset.Comp.SoundChannels.Remove(channel);
    }
}
