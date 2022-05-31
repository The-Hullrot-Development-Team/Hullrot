﻿using System.Diagnostics;
using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules.Configurations;
using Content.Server.Maps;
using Content.Server.Nuke;
using Content.Server.Players;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.MobState;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Maps;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.GameTicking.Rules;

public sealed class NukeopsRuleSystem : GameRuleSystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IMapLoader _mapLoader = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawningSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;

    private Dictionary<Mind.Mind, bool> _aliveNukeops = new();
    private bool _opsWon;

    public override string Prototype => "Nukeops";

    private const string NukeopsPrototypeId = "Nukeops";

    /// <summary>
    /// Config information for the current round
    /// </summary>
    private NukeopsGameRuleConfiguration _config = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartAttemptEvent>(OnStartAttempt);
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnPlayersSpawning);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
        SubscribeLocalEvent<NukeExplodedEvent>(OnNukeExploded);
    }

    private void OnNukeExploded(NukeExplodedEvent ev)
    {
    	if (!Enabled)
            return;

        _opsWon = true;
        _roundEndSystem.EndRound();
    }

    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        if (!Enabled)
            return;

        ev.AddLine(_opsWon ? Loc.GetString("nukeops-ops-won") : Loc.GetString("nukeops-crew-won"));
        ev.AddLine(Loc.GetString("nukeops-list-start"));
        foreach (var nukeop in _aliveNukeops)
        {
            ev.AddLine($"- {nukeop.Key.Session?.Name}");
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (!Enabled)
            return;

        if (!_aliveNukeops.TryFirstOrNull(x => x.Key.OwnedEntity == ev.Entity, out var op)) return;

        _aliveNukeops[op.Value.Key] = op.Value.Key.CharacterDeadIC;

        if (_aliveNukeops.Values.All(x => !x))
        {
            _roundEndSystem.EndRound();
        }
    }

    private void OnPlayersSpawning(RulePlayerSpawningEvent ev)
    {
        if (!Enabled)
            return;

        _aliveNukeops.Clear();

        // Basically copied verbatim from traitor code
        var playersPerOperative = _cfg.GetCVar(CCVars.NukeopsPlayersPerOp);
        var maxOperatives = _cfg.GetCVar(CCVars.NukeopsMaxOps);

        var list = new List<IPlayerSession>(ev.PlayerPool).Where(x =>
            x.Data.ContentData()?.Mind?.AllRoles.All(role => role is not Job {CanBeAntag: false}) ?? false
        ).ToList();
        var prefList = new List<IPlayerSession>();
        var operatives = new List<IPlayerSession>();

        foreach (var player in list)
        {
            if (!ev.Profiles.ContainsKey(player.UserId))
            {
                continue;
            }
            var profile = ev.Profiles[player.UserId];
            if (profile.AntagPreferences.Contains(NukeopsPrototypeId))
            {
                prefList.Add(player);
            }
        }

        var numNukies = MathHelper.Clamp(ev.PlayerPool.Count / playersPerOperative, 1, maxOperatives);

        for (var i = 0; i < numNukies; i++)
        {
            IPlayerSession nukeOp;
            if (prefList.Count == 0)
            {
                if (list.Count == 0)
                {
                    Logger.InfoS("preset", "Insufficient ready players to fill up with nukeops, stopping the selection");
                }
                nukeOp = _random.PickAndTake(list);
                Logger.InfoS("preset", "Insufficient preferred nukeops, picking at random.");
            }
            else
            {
                nukeOp = _random.PickAndTake(prefList);
                list.Remove(nukeOp);
                Logger.InfoS("preset", "Selected a preferred nukeop.");
            }
            operatives.Add(nukeOp);
        }

        string map;
        if (_config.Shuttles != null)
        {
            map = _random.Pick(_config.Shuttles).MapPath.ToString();
        }
        else
        {
            // Default to the Infiltrator
            map = "Maps/infiltrator.yml";
        }

        var aabbs = _stationSystem.Stations.SelectMany(x =>
            Comp<StationDataComponent>(x).Grids.Select(x => _mapManager.GetGridComp(x).Grid.WorldAABB)).ToArray();
        var aabb = aabbs[0];
        for (int i = 1; i < aabbs.Length; i++)
        {
            aabb.Union(aabbs[i]);
        }

        var (_, gridId) = _mapLoader.LoadBlueprint(GameTicker.DefaultMap, map, new MapLoadOptions
        {
            Offset = aabb.Center + MathF.Max(aabb.Height / 2f, aabb.Width / 2f) * 2.5f
        });

        if (!gridId.HasValue)
        {
            Logger.ErrorS("NUKEOPS", $"Gridid was null when loading \"{map}\", aborting.");
            foreach (var session in operatives)
            {
                ev.PlayerPool.Add(session);
            }
            return;
        }

        var gridUid = _mapManager.GetGridEuid(gridId.Value);
        StartingGearPrototype commanderGear;
        StartingGearPrototype medicGear;
        StartingGearPrototype starterGear;

        if (_config.Loadouts != null)
        {
            commanderGear = _random.Pick(_config.Loadouts["Commander"]);
            medicGear = _random.Pick(_config.Loadouts["Commander"]);
            starterGear = _random.Pick(_config.Loadouts["Commander"]);
        }
        else
        {
            commanderGear = _prototypeManager.Index<StartingGearPrototype>("SyndicateCommanderGearFull");
            medicGear = _prototypeManager.Index<StartingGearPrototype>("SyndicateOperativeMedicFull");
            starterGear = _prototypeManager.Index<StartingGearPrototype>("SyndicateCommanderGearFull");
        }

        var spawns = new List<EntityCoordinates>();

        // TODO: Don't hardcode prototypes
        foreach (var (_, meta, xform) in EntityManager.EntityQuery<SpawnPointComponent, MetaDataComponent, TransformComponent>(true))
        {
            if (meta.EntityPrototype?.ID != "SpawnPointNukies" || xform.ParentUid != gridUid) continue;

            spawns.Add(xform.Coordinates);
        }

        if (spawns.Count == 0)
        {
            spawns.Add(EntityManager.GetComponent<TransformComponent>(gridUid).Coordinates);
            Logger.WarningS("nukies", $"Fell back to default spawn for nukies!");
        }

        List<string> availableSpecies;
        if(_config.Species != null)
        {
            availableSpecies = _config.Species;
        }
        else
        {
            // Fall back to humans
            availableSpecies = new();
            availableSpecies.Add("MobHuman");
        }

        // TODO: This should spawn the nukies in regardless and transfer if possible; rest should go to shot roles.
        for (var i = 0; i < operatives.Count; i++)
        {
            string name;
            StartingGearPrototype gear;

            switch (i)
            {
                case 0:
                    name = $"Commander";
                    gear = commanderGear;
                    break;
                case 1:
                    name = $"Operator #{i}";
                    gear = medicGear;
                    break;
                default:
                    name = $"Operator #{i}";
                    gear = starterGear;
                    break;
            }

            var session = operatives[i];
            var newMind = new Mind.Mind(session.UserId)
            {
                CharacterName = name
            };
            newMind.ChangeOwningPlayer(session.UserId);
            var mob = EntityManager.SpawnEntity(_random.Pick(availableSpecies), _random.Pick(spawns));
            EntityManager.GetComponent<MetaDataComponent>(mob).EntityName = name;

            newMind.TransferTo(mob);
            _stationSpawningSystem.EquipStartingGear(mob, gear, null);

            _aliveNukeops.Add(newMind, true);

            GameTicker.PlayerJoinGame(session);
        }
    }

    private void OnStartAttempt(RoundStartAttemptEvent ev)
    {
        if (!Enabled)
            return;

        var minPlayers = _cfg.GetCVar(CCVars.NukeopsMinPlayers);
        if (!ev.Forced && ev.Players.Length < minPlayers)
        {
            _chatManager.DispatchServerAnnouncement(Loc.GetString("nukeops-not-enough-ready-players", ("readyPlayersCount", ev.Players.Length), ("minimumPlayers", minPlayers)));
            ev.Cancel();
            return;
        }

        if (ev.Players.Length == 0)
        {
            _chatManager.DispatchServerAnnouncement(Loc.GetString("nukeops-no-one-ready"));
            ev.Cancel();
            return;
        }
    }


    public override void Started(GameRuleConfiguration cfg)
    {
        _opsWon = false;
        _config = (NukeopsGameRuleConfiguration) cfg;
    }

    public override void Ended(GameRuleConfiguration _) { }
}
