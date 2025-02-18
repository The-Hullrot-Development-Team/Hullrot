using Content.Server.Chat.Systems;
using Content.Server.DeviceNetwork.Components;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StationAi;
using Content.Shared.Turrets;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using static Content.Server.Chat.Systems.ChatSystem;

namespace Content.Server.Silicons.StationAi;

public sealed class StationAiSystem : SharedStationAiSystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;

    private readonly HashSet<Entity<StationAiCoreComponent>> _stationAiCores = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandICChatRecipients);
        SubscribeLocalEvent<StationAiTurretComponent, AmmoShotEvent>(OnAmmoShot);
    }

    private void OnExpandICChatRecipients(ExpandICChatRecipientsEvent ev)
    {
        var xformQuery = GetEntityQuery<TransformComponent>();
        var sourceXform = Transform(ev.Source);
        var sourcePos = _xforms.GetWorldPosition(sourceXform, xformQuery);

        // This function ensures that chat popups appear on camera views that have connected microphones.
        var query = EntityManager.EntityQueryEnumerator<StationAiCoreComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var entStationAiCore, out var entXform))
        {
            var stationAiCore = new Entity<StationAiCoreComponent>(ent, entStationAiCore);

            if (!TryGetInsertedAI(stationAiCore, out var insertedAi) || !TryComp(insertedAi, out ActorComponent? actor))
                continue;

            if (stationAiCore.Comp.RemoteEntity == null || stationAiCore.Comp.Remote)
                continue;

            var xform = Transform(stationAiCore.Comp.RemoteEntity.Value);

            var range = (xform.MapID != sourceXform.MapID)
                ? -1
                : (sourcePos - _xforms.GetWorldPosition(xform, xformQuery)).Length();

            if (range < 0 || range > ev.VoiceRange)
                continue;

            ev.Recipients.TryAdd(actor.PlayerSession, new ICChatRecipientData(range, false));
        }
    }

    private void OnAmmoShot(Entity<StationAiTurretComponent> ent, ref AmmoShotEvent args)
    {
        var xform = Transform(ent);

        if (!TryComp(xform.GridUid, out MapGridComponent? grid))
            return;

        var ais = GetStationAIs(xform.GridUid.Value);

        foreach (var ai in ais)
        {
            var ev = new ChatNotificationEvent("TurretIsAttacking", ent);

            if (TryComp<DeviceNetworkComponent>(ent, out var deviceNetwork))
                ev.SourceNameOverride = Name(ent) + " (" + deviceNetwork.Address + ")";

            RaiseLocalEvent(ai, ref ev);
        }
    }

    public override bool SetVisionEnabled(Entity<StationAiVisionComponent> entity, bool enabled, bool announce = false)
    {
        if (!base.SetVisionEnabled(entity, enabled, announce))
            return false;

        if (announce)
            AnnounceSnip(entity.Owner);

        return true;
    }

    public override bool SetWhitelistEnabled(Entity<StationAiWhitelistComponent> entity, bool enabled, bool announce = false)
    {
        if (!base.SetWhitelistEnabled(entity, enabled, announce))
            return false;

        if (announce)
            AnnounceSnip(entity.Owner);

        return true;
    }

    private void AnnounceSnip(EntityUid uid)
    {
        var xform = Transform(uid);

        if (!TryComp(xform.GridUid, out MapGridComponent? grid))
            return;

        var ais = GetStationAIs(xform.GridUid.Value);

        foreach (var ai in ais)
        {
            if (!StationAiCanDetectWireSnipping(ai))
                continue;

            var ev = new ChatNotificationEvent("AiWireSnipped", uid);

            var tile = Maps.LocalToTile(xform.GridUid.Value, grid, xform.Coordinates);
            ev.SourceNameOverride = tile.ToString();

            RaiseLocalEvent(ai, ref ev);
        }
    }

    private bool StationAiCanDetectWireSnipping(EntityUid uid)
    {
        // TODO: The ability to detect snipped AI interaction wires
        // should be a MALF ability and/or a purchased upgrade rather
        // than something available to the station AI by default.
        // When these systems are added, add the appropriate checks here.

        return true;
    }

    public HashSet<EntityUid> GetStationAIs(EntityUid gridUid)
    {
        _stationAiCores.Clear();
        _lookup.GetChildEntities(gridUid, _stationAiCores);

        var hashSet = new HashSet<EntityUid>();

        foreach (var stationAiCore in _stationAiCores)
        {
            if (!TryGetInsertedAI(stationAiCore, out var insertedAi))
                continue;

            hashSet.Add(insertedAi.Value);
        }

        return hashSet;
    }
}
