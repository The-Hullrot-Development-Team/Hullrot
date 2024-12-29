using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Speech.Components;
using Content.Server.Telephone;
using Content.Shared.Access.Systems;
using Content.Shared.Audio;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.Holopad;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Labels.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Telephone;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server.Holopad;

public sealed class HolopadSystem : SharedHolopadSystem
{
    [Dependency] private readonly TelephoneSystem _telephoneSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private readonly TransformSystem _xformSystem = default!;
    [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLightSystem = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSoundSystem = default!;
    [Dependency] private readonly SharedStationAiSystem _stationAiSystem = default!;
    [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private float _updateTimer = 1.0f;
    private const float UpdateTime = 1.0f;

    public override void Initialize()
    {
        base.Initialize();

        // Holopad UI and bound user interface messages
        SubscribeLocalEvent<HolopadComponent, BeforeActivatableUIOpenEvent>(OnUIOpen);
        SubscribeLocalEvent<HolopadComponent, HolopadStartNewCallMessage>(OnHolopadStartNewCall);
        SubscribeLocalEvent<HolopadComponent, HolopadAnswerCallMessage>(OnHolopadAnswerCall);
        SubscribeLocalEvent<HolopadComponent, HolopadEndCallMessage>(OnHolopadEndCall);
        SubscribeLocalEvent<HolopadComponent, HolopadActivateProjectorMessage>(OnHolopadActivateProjector);
        SubscribeLocalEvent<HolopadComponent, HolopadStartBroadcastMessage>(OnHolopadStartBroadcast);
        SubscribeLocalEvent<HolopadComponent, HolopadStationAiRequestMessage>(OnHolopadStationAiRequest);

        // Holopad telephone events
        SubscribeLocalEvent<HolopadComponent, TelephoneStateChangeEvent>(OnTelephoneStateChange);
        SubscribeLocalEvent<HolopadComponent, TelephoneCallCommencedEvent>(OnHoloCallCommenced);
        SubscribeLocalEvent<HolopadComponent, TelephoneCallEndedEvent>(OnHoloCallEnded);
        SubscribeLocalEvent<HolopadComponent, TelephoneMessageSentEvent>(OnTelephoneMessageSent);

        // Networked events
        SubscribeNetworkEvent<HolopadUserTypingChangedEvent>(OnTypingChanged);
        SubscribeLocalEvent<ExpandPvsEvent>(OnExpandPvs);
        SubscribeLocalEvent<HolopadHologramComponent, ComponentGetState>(GetHolopadHologramState);

        // Component start/shutdown events
        SubscribeLocalEvent<HolopadComponent, ComponentInit>(OnHolopadInit);
        SubscribeLocalEvent<HolopadComponent, ComponentShutdown>(OnHolopadShutdown);
        SubscribeLocalEvent<HolopadUserComponent, ComponentInit>(OnHolopadUserInit);
        SubscribeLocalEvent<HolopadUserComponent, ComponentShutdown>(OnHolopadUserShutdown);

        // Misc events
        SubscribeLocalEvent<HolopadUserComponent, EmoteEvent>(OnEmote);
        SubscribeLocalEvent<HolopadUserComponent, JumpToCoreEvent>(OnJumpToCore);
        SubscribeLocalEvent<HolopadComponent, GetVerbsEvent<AlternativeVerb>>(AddToggleProjectorVerb);
        SubscribeLocalEvent<HolopadComponent, EntRemovedFromContainerMessage>(OnAiRemove);
    }

    #region: Holopad UI bound user interface messages

    private void OnUIOpen(Entity<HolopadComponent> entity, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateUIState(entity);
    }

    private void OnHolopadStartNewCall(Entity<HolopadComponent> source, ref HolopadStartNewCallMessage args)
    {
        if (IsHolopadControlLocked(source, args.Actor))
            return;

        if (!TryComp<TelephoneComponent>(source, out var sourceTelephone))
            return;

        var receiver = GetEntity(args.Receiver);

        if (!TryComp<TelephoneComponent>(receiver, out var receiverTelephone))
            return;

        LinkHolopadToUser(source, args.Actor);
        _telephoneSystem.CallTelephone((source, sourceTelephone), (receiver, receiverTelephone), args.Actor);
    }

    private void OnHolopadAnswerCall(Entity<HolopadComponent> receiver, ref HolopadAnswerCallMessage args)
    {
        if (IsHolopadControlLocked(receiver, args.Actor))
            return;

        if (!TryComp<TelephoneComponent>(receiver, out var receiverTelephone))
            return;

        if (TryComp<StationAiHeldComponent>(args.Actor, out var userAiHeld))
        {
            var source = GetLinkedHolopads(receiver).FirstOrNull();

            if (source != null)
            {
                // Close any AI request windows
                if (_stationAiSystem.TryGetStationAiCore(args.Actor, out var stationAiCore) && stationAiCore != null)
                    _userInterfaceSystem.CloseUi(receiver.Owner, HolopadUiKey.AiRequestWindow, args.Actor);

                // Try to warn the AI if the source of the call is out of its range
                if (TryComp<TelephoneComponent>(stationAiCore, out var stationAiTelephone) &&
                    TryComp<TelephoneComponent>(source, out var sourceTelephone) &&
                    !_telephoneSystem.IsSourceInRangeOfReceiver((stationAiCore.Value.Owner, stationAiTelephone), (source.Value.Owner, sourceTelephone)))
                {
                    _popupSystem.PopupEntity(Loc.GetString("holopad-ai-is-unable-to-reach-holopad"), receiver, args.Actor);
                    return;
                }

                ActivateProjector(source.Value, args.Actor);
            }

            return;
        }

        LinkHolopadToUser(receiver, args.Actor);
        _telephoneSystem.AnswerTelephone((receiver, receiverTelephone), args.Actor);
    }

    private void OnHolopadEndCall(Entity<HolopadComponent> entity, ref HolopadEndCallMessage args)
    {
        if (!TryComp<TelephoneComponent>(entity, out var entityTelephone))
            return;

        if (IsHolopadControlLocked(entity, args.Actor))
            return;

        _telephoneSystem.EndTelephoneCalls((entity, entityTelephone));

        // If the user is an AI, end all calls originating from its
        // associated core to ensure that any broadcasts will end
        if (!TryComp<StationAiHeldComponent>(args.Actor, out var stationAiHeld) ||
            !_stationAiSystem.TryGetStationAiCore((args.Actor, stationAiHeld), out var stationAiCore))
            return;

        if (TryComp<TelephoneComponent>(stationAiCore, out var telephone))
            _telephoneSystem.EndTelephoneCalls((stationAiCore.Value, telephone));
    }

    private void OnHolopadActivateProjector(Entity<HolopadComponent> entity, ref HolopadActivateProjectorMessage args)
    {
        ActivateProjector(entity, args.Actor);
    }

    private void OnHolopadStartBroadcast(Entity<HolopadComponent> source, ref HolopadStartBroadcastMessage args)
    {
        if (IsHolopadControlLocked(source, args.Actor) || IsHolopadBroadcastOnCoolDown(source))
            return;

        if (!_accessReaderSystem.IsAllowed(args.Actor, source))
            return;

        // AI broadcasting
        if (TryComp<StationAiHeldComponent>(args.Actor, out var stationAiHeld))
        {
            if (!_stationAiSystem.TryGetStationAiCore((args.Actor, stationAiHeld), out var stationAiCore) ||
                stationAiCore.Value.Comp.RemoteEntity == null ||
                !TryComp<HolopadComponent>(stationAiCore, out var stationAiCoreHolopad))
                return;

            ExecuteBroadcast((stationAiCore.Value, stationAiCoreHolopad), args.Actor);

            // Switch the AI's perspective from free roaming to the target holopad
            _xformSystem.SetCoordinates(stationAiCore.Value.Comp.RemoteEntity.Value, Transform(source).Coordinates);
            _stationAiSystem.SwitchRemoteEntityMode(stationAiCore.Value, false);

            return;
        }

        // Crew broadcasting
        ExecuteBroadcast(source, args.Actor);
    }

    private void OnHolopadStationAiRequest(Entity<HolopadComponent> entity, ref HolopadStationAiRequestMessage args)
    {
        if (IsHolopadControlLocked(entity, args.Actor))
            return;

        if (!TryComp<TelephoneComponent>(entity, out var telephone))
            return;

        var source = new Entity<TelephoneComponent>(entity, telephone);
        var query = AllEntityQuery<StationAiCoreComponent, TelephoneComponent>();
        var reachableAiCores = new HashSet<Entity<TelephoneComponent>>();

        while (query.MoveNext(out var receiverUid, out var receiverStationAiCore, out var receiverTelephone))
        {
            var receiver = new Entity<TelephoneComponent>(receiverUid, receiverTelephone);

            // Check if the core can reach the call source, rather than the other way around
            if (!_telephoneSystem.IsSourceAbleToReachReceiver(receiver, source))
                continue;

            if (_telephoneSystem.IsTelephoneEngaged(receiver))
                continue;

            reachableAiCores.Add((receiverUid, receiverTelephone));

            if (!_stationAiSystem.TryGetInsertedAI((receiver, receiverStationAiCore), out var insertedAi))
                continue;

            if (_userInterfaceSystem.TryOpenUi(receiverUid, HolopadUiKey.AiRequestWindow, insertedAi.Value.Owner))
                LinkHolopadToUser(entity, args.Actor);
        }

        // Ignore range so that holopads that ignore other devices on the same grid can request the AI
        var options = new TelephoneCallOptions { IgnoreRange = true };
        _telephoneSystem.BroadcastCallToTelephones(source, reachableAiCores, args.Actor, options);
    }

    #endregion

    #region: Holopad telephone events

    private void OnTelephoneStateChange(Entity<HolopadComponent> holopad, ref TelephoneStateChangeEvent args)
    {
        // Update holopad visual and ambient states
        switch (args.NewState)
        {
            case TelephoneState.Idle:
                ShutDownHolopad(holopad);
                SetHolopadAmbientState(holopad, false);
                break;

            case TelephoneState.EndingCall:
                ShutDownHolopad(holopad);
                break;

            default:
                SetHolopadAmbientState(holopad, this.IsPowered(holopad, EntityManager));
                break;
        }
    }

    private void OnHoloCallCommenced(Entity<HolopadComponent> source, ref TelephoneCallCommencedEvent args)
    {
        if (source.Comp.Hologram == null)
            GenerateHologram(source);

        if (TryComp<HolopadComponent>(args.Receiver, out var receivingHolopad) && receivingHolopad.Hologram == null)
            GenerateHologram((args.Receiver, receivingHolopad));

        // Re-link the user to refresh the sprite data
        if (source.Comp.User != null)
            LinkHolopadToUser(source, source.Comp.User.Value);
    }

    private void OnHoloCallEnded(Entity<HolopadComponent> entity, ref TelephoneCallEndedEvent args)
    {
        if (!TryComp<StationAiCoreComponent>(entity, out var stationAiCore))
            return;

        // Auto-close the AI request window
        if (_stationAiSystem.TryGetInsertedAI((entity, stationAiCore), out var insertedAi))
            _userInterfaceSystem.CloseUi(entity.Owner, HolopadUiKey.AiRequestWindow, insertedAi.Value.Owner);
    }

    private void OnTelephoneMessageSent(Entity<HolopadComponent> holopad, ref TelephoneMessageSentEvent args)
    {
        LinkHolopadToUser(holopad, args.MessageSource);
    }

    #endregion

    #region: Networked events

    private void OnTypingChanged(HolopadUserTypingChangedEvent ev, EntitySessionEventArgs args)
    {
        var uid = args.SenderSession.AttachedEntity;

        if (!Exists(uid))
            return;

        if (!TryComp<HolopadUserComponent>(uid, out var holopadUser))
            return;

        foreach (var linkedHolopad in holopadUser.LinkedHolopads)
        {
            var receiverHolopads = GetLinkedHolopads(linkedHolopad);

            foreach (var receiverHolopad in receiverHolopads)
            {
                if (receiverHolopad.Comp.Hologram == null)
                    continue;

                _appearanceSystem.SetData(receiverHolopad.Comp.Hologram.Value.Owner, TypingIndicatorVisuals.IsTyping, ev.IsTyping);
            }
        }
    }

    private void OnExpandPvs(ref ExpandPvsEvent args)
    {
        if (args.Entities == null)
            args.Entities = new();

        // Add holopad users and their held/equipped items to PVS so that they can be rendered by remote holopads
        var query = AllEntityQuery<HolopadUserComponent>();
        while (query.MoveNext(out var ent, out var entHolopadUser))
        {
            args.Entities.Add(ent);

            foreach (var item in _inventorySystem.GetHandOrInventoryEntities(ent))
                args.Entities.Add(item);
        }
    }

    private void GetHolopadHologramState(Entity<HolopadHologramComponent> entity, ref ComponentGetState args)
    {
        var netTarget = GetNetEntity(entity.Comp.LinkedEntity);

        args.State = new HolopadHologramComponentState(netTarget);
    }

    #endregion

    #region: Component start/shutdown events

    private void OnHolopadInit(Entity<HolopadComponent> entity, ref ComponentInit args)
    {
        if (entity.Comp.User != null)
            LinkHolopadToUser(entity, entity.Comp.User.Value);
    }

    private void OnHolopadUserInit(Entity<HolopadUserComponent> entity, ref ComponentInit args)
    {
        foreach (var linkedHolopad in entity.Comp.LinkedHolopads)
            LinkHolopadToUser(linkedHolopad, entity);
    }

    private void OnHolopadShutdown(Entity<HolopadComponent> entity, ref ComponentShutdown args)
    {
        if (TryComp<TelephoneComponent>(entity, out var telphone) && _telephoneSystem.IsTelephoneEngaged((entity.Owner, telphone)))
            _telephoneSystem.EndTelephoneCalls((entity, telphone));

        ShutDownHolopad(entity);
        SetHolopadAmbientState(entity, false);
    }

    private void OnHolopadUserShutdown(Entity<HolopadUserComponent> entity, ref ComponentShutdown args)
    {
        foreach (var linkedHolopad in entity.Comp.LinkedHolopads)
            UnlinkHolopadFromUser(linkedHolopad, entity);
    }

    #endregion

    #region: Misc events

    private void OnEmote(Entity<HolopadUserComponent> entity, ref EmoteEvent args)
    {
        foreach (var linkedHolopad in entity.Comp.LinkedHolopads)
        {
            // Treat the ability to hear speech as the ability to also perceive emotes
            // (these are almost always going to be linked)
            if (!HasComp<ActiveListenerComponent>(linkedHolopad))
                continue;

            if (TryComp<TelephoneComponent>(linkedHolopad, out var linkedHolopadTelephone) && linkedHolopadTelephone.Muted)
                continue;

            foreach (var receiver in GetLinkedHolopads(linkedHolopad))
            {
                if (receiver.Comp.Hologram == null)
                    continue;

                // Name is based on the physical identity of the user
                var ent = Identity.Entity(entity, EntityManager);
                var name = Loc.GetString("holopad-hologram-name", ("name", ent));

                // Force the emote, because if the user can do it, the hologram can too
                _chatSystem.TryEmoteWithChat(receiver.Comp.Hologram.Value, args.Emote, ChatTransmitRange.Normal, false, name, true, true);
            }
        }
    }

    private void OnJumpToCore(Entity<HolopadUserComponent> entity, ref JumpToCoreEvent args)
    {
        if (!TryComp<StationAiHeldComponent>(entity, out var entityStationAiHeld))
            return;

        if (!_stationAiSystem.TryGetStationAiCore((entity, entityStationAiHeld), out var stationAiCore))
            return;

        if (!TryComp<TelephoneComponent>(stationAiCore, out var stationAiCoreTelephone))
            return;

        _telephoneSystem.EndTelephoneCalls((stationAiCore.Value, stationAiCoreTelephone));
    }

    private void AddToggleProjectorVerb(Entity<HolopadComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!this.IsPowered(entity, EntityManager))
            return;

        if (!TryComp<TelephoneComponent>(entity, out var entityTelephone) ||
            _telephoneSystem.IsTelephoneEngaged((entity, entityTelephone)))
            return;

        var user = args.User;

        if (!TryComp<StationAiHeldComponent>(user, out var userAiHeld))
            return;

        if (!_stationAiSystem.TryGetStationAiCore((user, userAiHeld), out var stationAiCore) ||
            stationAiCore.Value.Comp.RemoteEntity == null)
            return;

        AlternativeVerb verb = new()
        {
            Act = () => ActivateProjector(entity, user),
            Text = Loc.GetString("holopad-activate-projector-verb"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/vv.svg.192dpi.png")),
        };

        args.Verbs.Add(verb);
    }

    private void OnAiRemove(Entity<HolopadComponent> entity, ref EntRemovedFromContainerMessage args)
    {
        if (!HasComp<StationAiCoreComponent>(entity))
            return;

        if (!TryComp<TelephoneComponent>(entity, out var entityTelephone))
            return;

        _telephoneSystem.EndTelephoneCalls((entity, entityTelephone));
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;

        if (_updateTimer >= UpdateTime)
        {
            _updateTimer -= UpdateTime;

            var query = AllEntityQuery<HolopadComponent, TelephoneComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var holopad, out var telephone, out var xform))
            {
                UpdateUIState((uid, holopad), telephone);

                if (holopad.User != null &&
                    !HasComp<IgnoreUIRangeComponent>(holopad.User) &&
                    !_xformSystem.InRange((holopad.User.Value, Transform(holopad.User.Value)), (uid, xform), telephone.ListeningRange))
                {
                    UnlinkHolopadFromUser((uid, holopad), holopad.User.Value);
                }
            }
        }
    }

    public void UpdateUIState(Entity<HolopadComponent> entity, TelephoneComponent? telephone = null)
    {
        if (!Resolve(entity.Owner, ref telephone, false))
            return;

        var source = new Entity<TelephoneComponent>(entity, telephone);
        var holopads = new Dictionary<NetEntity, string>();

        var query = AllEntityQuery<HolopadComponent, TelephoneComponent>();
        while (query.MoveNext(out var receiverUid, out var _, out var receiverTelephone))
        {
            var receiver = new Entity<TelephoneComponent>(receiverUid, receiverTelephone);

            if (receiverTelephone.UnlistedNumber)
                continue;

            if (source == receiver)
                continue;

            if (!_telephoneSystem.IsSourceInRangeOfReceiver(source, receiver))
                continue;

            var name = MetaData(receiverUid).EntityName;

            if (TryComp<LabelComponent>(receiverUid, out var label) && !string.IsNullOrEmpty(label.CurrentLabel))
                name = label.CurrentLabel;

            holopads.Add(GetNetEntity(receiverUid), name);
        }

        var uiKey = HasComp<StationAiCoreComponent>(entity) ? HolopadUiKey.AiActionWindow : HolopadUiKey.InteractionWindow;
        _userInterfaceSystem.SetUiState(entity.Owner, uiKey, new HolopadBoundInterfaceState(holopads));
    }

    private void GenerateHologram(Entity<HolopadComponent> entity)
    {
        if (entity.Comp.Hologram != null ||
            entity.Comp.HologramProtoId == null)
            return;

        var uid = Spawn(entity.Comp.HologramProtoId, Transform(entity).Coordinates);

        // Safeguard - spawned holograms must have this component
        if (!TryComp<HolopadHologramComponent>(uid, out var component))
        {
            Del(uid);
            return;
        }

        entity.Comp.Hologram = new Entity<HolopadHologramComponent>(uid, component);
    }

    private void DeleteHologram(Entity<HolopadHologramComponent> hologram, Entity<HolopadComponent> attachedHolopad)
    {
        attachedHolopad.Comp.Hologram = null;

        QueueDel(hologram);
    }

    private void LinkHolopadToUser(Entity<HolopadComponent> entity, EntityUid user)
    {
        if (!TryComp<HolopadUserComponent>(user, out var holopadUser))
            holopadUser = AddComp<HolopadUserComponent>(user);

        if (user != entity.Comp.User?.Owner)
        {
            // Removes the old user from the holopad
            UnlinkHolopadFromUser(entity, entity.Comp.User);

            // Assigns the new user in their place
            holopadUser.LinkedHolopads.Add(entity);
            entity.Comp.User = (user, holopadUser);
        }

        foreach (var receiver in GetLinkedHolopads(entity))
            SyncHolopadHologramAppearanceWithTarget(receiver, (user, holopadUser));
    }

    private void SyncHolopadHologramAppearanceWithTarget(Entity<HolopadComponent> entity, Entity<HolopadUserComponent>? user)
    {
        var netHologram = GetNetEntity(entity.Comp.Hologram);

        if (netHologram == null)
            return;

        var netUser = GetNetEntity(user);

        entity.Comp.Hologram!.Value.Comp.LinkedEntity = user?.Owner;
        Dirty(entity.Comp.Hologram.Value);
    }

    private void UnlinkHolopadFromUser(Entity<HolopadComponent> entity, Entity<HolopadUserComponent>? user)
    {
        if (user == null)
            return;

        entity.Comp.User = null;

        foreach (var linkedHolopad in GetLinkedHolopads(entity))
        {
            if (linkedHolopad.Comp.Hologram != null)
            {
                _appearanceSystem.SetData(linkedHolopad.Comp.Hologram.Value.Owner, TypingIndicatorVisuals.IsTyping, false);
                SyncHolopadHologramAppearanceWithTarget(linkedHolopad, null);
            }
        }

        if (!HasComp<HolopadUserComponent>(user))
            return;

        user.Value.Comp.LinkedHolopads.Remove(entity);

        if (!user.Value.Comp.LinkedHolopads.Any() &&
            user.Value.Comp.LifeStage < ComponentLifeStage.Stopping)
        {
            RemComp<HolopadUserComponent>(user.Value);
        }
    }

    private void ShutDownHolopad(Entity<HolopadComponent> entity)
    {
        entity.Comp.ControlLockoutOwner = null;

        if (entity.Comp.Hologram != null)
            DeleteHologram(entity.Comp.Hologram.Value, entity);

        if (entity.Comp.User != null)
            UnlinkHolopadFromUser(entity, entity.Comp.User.Value);

        if (TryComp<StationAiCoreComponent>(entity, out var stationAiCore))
            _stationAiSystem.SwitchRemoteEntityMode((entity.Owner, stationAiCore), true);

        Dirty(entity);
    }

    private void ActivateProjector(Entity<HolopadComponent> entity, EntityUid user)
    {
        if (!TryComp<TelephoneComponent>(entity, out var receiverTelephone))
            return;

        var receiver = new Entity<TelephoneComponent>(entity, receiverTelephone);

        if (!TryComp<StationAiHeldComponent>(user, out var userAiHeld))
            return;

        if (!_stationAiSystem.TryGetStationAiCore((user, userAiHeld), out var stationAiCore) ||
            stationAiCore.Value.Comp.RemoteEntity == null)
            return;

        if (!TryComp<TelephoneComponent>(stationAiCore, out var stationAiTelephone))
            return;

        if (!TryComp<HolopadComponent>(stationAiCore, out var stationAiHolopad))
            return;

        var source = new Entity<TelephoneComponent>(stationAiCore.Value, stationAiTelephone);

        // Check if the AI is unable to activate the projector (unlikely this will ever pass; its just a safeguard)
        if (!_telephoneSystem.IsSourceInRangeOfReceiver(source, receiver))
        {
            _popupSystem.PopupEntity(Loc.GetString("holopad-ai-is-unable-to-activate-projector"), receiver, user);
            return;
        }

        // Terminate any calls that the core is hosting and immediately connect to the receiver
        _telephoneSystem.TerminateTelephoneCalls(source);

        var callOptions = new TelephoneCallOptions()
        {
            ForceConnect = true,
            MuteReceiver = true
        };

        _telephoneSystem.CallTelephone(source, receiver, user, callOptions);

        if (!_telephoneSystem.IsSourceConnectedToReceiver(source, receiver))
            return;

        LinkHolopadToUser((stationAiCore.Value, stationAiHolopad), user);

        // Switch the AI's perspective from free roaming to the target holopad
        _xformSystem.SetCoordinates(stationAiCore.Value.Comp.RemoteEntity.Value, Transform(entity).Coordinates);
        _stationAiSystem.SwitchRemoteEntityMode(stationAiCore.Value, false);

        // Open the holopad UI if it hasn't been opened yet
        if (TryComp<UserInterfaceComponent>(entity, out var entityUserInterfaceComponent))
            _userInterfaceSystem.OpenUi((entity, entityUserInterfaceComponent), HolopadUiKey.InteractionWindow, user);
    }

    private void ExecuteBroadcast(Entity<HolopadComponent> source, EntityUid user)
    {
        if (!TryComp<TelephoneComponent>(source, out var sourceTelephone))
            return;

        var sourceTelephoneEntity = new Entity<TelephoneComponent>(source, sourceTelephone);
        _telephoneSystem.TerminateTelephoneCalls(sourceTelephoneEntity);

        // Find all holopads in range of the source
        var sourceXform = Transform(source);
        var receivers = new HashSet<Entity<TelephoneComponent>>();

        var query = AllEntityQuery<HolopadComponent, TelephoneComponent, TransformComponent>();
        while (query.MoveNext(out var receiver, out var receiverHolopad, out var receiverTelephone, out var receiverXform))
        {
            var receiverTelephoneEntity = new Entity<TelephoneComponent>(receiver, receiverTelephone);

            if (sourceTelephoneEntity == receiverTelephoneEntity ||
                !_telephoneSystem.IsSourceAbleToReachReceiver(sourceTelephoneEntity, receiverTelephoneEntity))
                continue;

            // If any holopads in range are on broadcast cooldown, exit
            if (IsHolopadBroadcastOnCoolDown((receiver, receiverHolopad)))
                return;

            receivers.Add(receiverTelephoneEntity);
        }

        var options = new TelephoneCallOptions()
        {
            ForceConnect = true,
            MuteReceiver = true,
        };

        _telephoneSystem.BroadcastCallToTelephones(sourceTelephoneEntity, receivers, user, options);

        if (!_telephoneSystem.IsTelephoneEngaged(sourceTelephoneEntity))
            return;

        // Link to the user after all the calls have been placed,
        // so we only need to sync all the holograms once
        LinkHolopadToUser(source, user);

        // Lock out the controls of all involved holopads for a set duration
        source.Comp.ControlLockoutOwner = user;
        source.Comp.ControlLockoutStartTime = _timing.CurTime;

        Dirty(source);

        foreach (var receiver in GetLinkedHolopads(source))
        {
            receiver.Comp.ControlLockoutOwner = user;
            receiver.Comp.ControlLockoutStartTime = _timing.CurTime;

            Dirty(receiver);
        }
    }

    private HashSet<Entity<HolopadComponent>> GetLinkedHolopads(Entity<HolopadComponent> entity)
    {
        var linkedHolopads = new HashSet<Entity<HolopadComponent>>();

        if (!TryComp<TelephoneComponent>(entity, out var holopadTelephone))
            return linkedHolopads;

        foreach (var linkedEnt in holopadTelephone.LinkedTelephones)
        {
            if (!TryComp<HolopadComponent>(linkedEnt, out var linkedHolopad))
                continue;

            linkedHolopads.Add((linkedEnt, linkedHolopad));
        }

        return linkedHolopads;
    }

    private void SetHolopadAmbientState(Entity<HolopadComponent> entity, bool isEnabled)
    {
        if (TryComp<PointLightComponent>(entity, out var pointLight))
            _pointLightSystem.SetEnabled(entity, isEnabled, pointLight);

        if (TryComp<AmbientSoundComponent>(entity, out var ambientSound))
            _ambientSoundSystem.SetAmbience(entity, isEnabled, ambientSound);
    }
}
