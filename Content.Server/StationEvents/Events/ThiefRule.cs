using Content.Server.Administration.Commands;
using Content.Server.Communications;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Roles;
using Content.Server.GenericAntag;
using Content.Shared.Alert;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Doors.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.Rounding;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;
using System.Diagnostics.CodeAnalysis;
using Content.Server.Objectives.Components;

using System.Linq;
using Content.Server.Mind;
using Content.Server.NPC.Systems;
using Content.Server.Objectives;
using Content.Server.PDA.Ringer;
using Content.Server.Shuttles.Components;
using Content.Shared.CCVar;
using Content.Shared.Dataset;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.PDA;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.StationEvents.Events;

/// <summary>
/// Event for spawning a Thief. Auto invoke on start round in Suitable game modes, or can be invoked in mid-game.
/// </summary>
public sealed class ThiefRule : StationEventSystem<ThiefRuleComponent>
{
    [Dependency] private readonly RoleSystem _role = default!;

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly SharedRoleSystem _roleSystem = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;

    [Dependency] private readonly GameTicker _gameTicker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(HandleLatejoin);
    }

    protected override void Started(EntityUid uid, ThiefRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);
        Log.Error("------------ Start Thief Event ------------");

        var thiefPool = FindPotentialThiefs(comp.StartCandidates, comp);
    }

    private List<ICommonSession> FindPotentialThiefs(in Dictionary<ICommonSession, HumanoidCharacterProfile> candidates, ThiefRuleComponent component)
    {
        var list = new List<ICommonSession>();
        var pendingQuery = GetEntityQuery<PendingClockInComponent>();

        foreach (var player in candidates.Keys)
        {
            // Role prevents antag.
            if (!_jobs.CanBeAntag(player))
                continue;

            // Latejoin
            if (player.AttachedEntity != null && pendingQuery.HasComponent(player.AttachedEntity.Value))
                continue;

            list.Add(player);
        }

        var prefList = new List<ICommonSession>();

        foreach (var player in list)
        {
            //player preferences to play as thief
            var profile = candidates[player];
            if (profile.AntagPreferences.Contains(component.ThiefPrototypeId))
            {
                prefList.Add(player);
            }
        }
        if (prefList.Count == 0)
        {
            Log.Info("Insufficient preferred thiefs, picking at random.");
            prefList = list;
        }
        return prefList;
    }



    private void HandleLatejoin(PlayerSpawnCompleteEvent ev) //Это кстати сработало и при раундстарт подключении. До OnPlayerSpawned
    {
        //Если по какой-то причине текущее колиество воров еще недостаточно, тут мы добираем игроков

        //Log.Error("---------------- HandleLatejoin " + ev.Player.Name);
        //foreach (var r in _gameTicker.GetAddedGameRules())
        //{
        //    Log.Error("----- Rule: " + r.ToString());
        //}
    }

    public void MakeThief(ICommonSession thief)
    {
        var thiefRule = EntityQuery<ThiefRuleComponent>().FirstOrDefault();
        if (thiefRule == null)
        {
            //todo fuck me this shit is awful
            //no i wont fuck you, erp is against rules
            GameTicker.StartGameRule("Thief", out var ruleEntity);
            thiefRule = Comp<ThiefRuleComponent>(ruleEntity);
        }

        Log.Error(thief.Name + "is now thief!");
        _audioSystem.PlayGlobal(thiefRule.GreetingSound, thief);
    }

    /// <summary>
    /// Returns a thief's gamerule config data.
    /// If the gamerule was not started then it will be started automatically.
    /// </summary>
    public ThiefRuleComponent? GetThiefRule(EntityUid uid, GenericAntagComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return null;

        // mind not added yet so no rule
        if (comp.RuleEntity == null)
            return null;

        return CompOrNull<ThiefRuleComponent>(comp.RuleEntity);
    }


}
