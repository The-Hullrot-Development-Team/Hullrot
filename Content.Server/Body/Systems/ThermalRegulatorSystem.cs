using Content.Server.Body.Components;
using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;
using Content.Shared.ActionBlocker;
using Robust.Shared.Timing;
using Content.Server.Chat.Systems;
using Content.Shared.Mobs.Systems;

namespace Content.Server.Body.Systems;

public sealed class ThermalRegulatorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly TemperatureSystem _tempSys = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSys = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThermalRegulatorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ThermalRegulatorComponent, EntityUnpausedEvent>(OnUnpaused);
    }

    private void OnMapInit(Entity<ThermalRegulatorComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextUpdate = _gameTiming.CurTime + ent.Comp.UpdateInterval;
    }

    private void OnUnpaused(Entity<ThermalRegulatorComponent> ent, ref EntityUnpausedEvent args)
    {
        ent.Comp.NextUpdate += args.PausedTime;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ThermalRegulatorComponent>();
        while (query.MoveNext(out var uid, out var regulator))
        {
            if (_gameTiming.CurTime < regulator.NextUpdate)
                continue;

            regulator.NextUpdate += regulator.UpdateInterval;
            ProcessThermalRegulation((uid, regulator));
        }
    }

    /// <summary>
    /// Processes thermal regulation for a mob
    /// </summary>
    private void ProcessThermalRegulation(Entity<ThermalRegulatorComponent, TemperatureComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp2, logMissing: false))
            return;

        if (_mobState.IsDead(ent))
            return;

        var totalMetabolismTempChange = ent.Comp1.MetabolismHeat - ent.Comp1.RadiatedHeat;

        // implicit heat regulation
        var tempDiff = Math.Abs(ent.Comp2.CurrentTemperature - ent.Comp1.NormalBodyTemperature);
        var heatCapacity = _tempSys.GetHeatCapacity(ent, ent);
        var targetHeat = tempDiff * heatCapacity;
        if (ent.Comp2.CurrentTemperature > ent.Comp1.NormalBodyTemperature)
        {
            totalMetabolismTempChange -= Math.Min(targetHeat, ent.Comp1.ImplicitHeatRegulation);
        }
        else
        {
            totalMetabolismTempChange += Math.Min(targetHeat, ent.Comp1.ImplicitHeatRegulation);
        }

        _tempSys.ChangeHeat(ent, totalMetabolismTempChange, ignoreHeatResistance: true, ent);

        // recalc difference and target heat
        tempDiff = Math.Abs(ent.Comp2.CurrentTemperature - ent.Comp1.NormalBodyTemperature);
        targetHeat = tempDiff * heatCapacity;

        // if body temperature is not within comfortable, thermal regulation
        // processes starts
        if (tempDiff < ent.Comp1.ThermalRegulationTemperatureThreshold)
            return;

        if (ent.Comp2.CurrentTemperature > ent.Comp1.NormalBodyTemperature)
        {
            if (!_actionBlockerSys.CanSweat(ent))
                return;

            _tempSys.ChangeHeat(ent, -Math.Min(targetHeat, ent.Comp1.SweatHeatRegulation), ignoreHeatResistance: true, ent);

            if (!ent.Comp1.VisuallySweats)
                return;

            //for humans, they start sweating at 25C over body temp, at once every 30 seconds, and maximally at 75C over, once per 15 seconds
            ent.Comp1.SweatEmoteProgress += Math.Min(tempDiff / ent.Comp1.ThermalRegulationTemperatureThreshold, 2) / 30;
            if (ent.Comp1.SweatEmoteProgress > 1.0f)
            {
                _chat.TryEmoteWithChat(ent, ent.Comp1.SweatEmote, ChatTransmitRange.HideChat, ignoreActionBlocker: true);
                ent.Comp1.SweatEmoteProgress = 0.0f;
            }
        }
        else
        {
            if (!_actionBlockerSys.CanShiver(ent))
                return;

            _tempSys.ChangeHeat(ent, Math.Min(targetHeat, ent.Comp1.ShiveringHeatRegulation), ignoreHeatResistance: true, ent);

            if (!ent.Comp1.VisuallyShivers)
                return;

            //for humans, they start shivering at 25C under body temp, at once every 30 seconds, and maximally at 75C under, once per 15 seconds
            ent.Comp1.ShiverEmoteProgress += Math.Min(tempDiff / ent.Comp1.ThermalRegulationTemperatureThreshold, 2) / 30;
            if (ent.Comp1.ShiverEmoteProgress > 1.0f)
            {
                _chat.TryEmoteWithChat(ent, ent.Comp1.ShiverEmote, ChatTransmitRange.HideChat, ignoreActionBlocker: true);
                ent.Comp1.ShiverEmoteProgress = 0.0f;
            }
        }
    }
}
