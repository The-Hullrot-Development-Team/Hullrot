﻿using Content.Client.Alerts;
using Content.Client.Gameplay;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.UserInterface.Systems.DamageOverlays;

[UsedImplicitly]
public sealed class DamageOverlayUiController : UIController, IOnStateChanged<GameplayState>
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    [UISystemDependency] private readonly ClientAlertsSystem _alertsSystem = default!;
    [UISystemDependency] private readonly MobThresholdSystem _mobThresholdSystem = default!;
    private Overlays.DamageOverlay _overlay = default!;

    public override void Initialize()
    {
        _overlay = new Overlays.DamageOverlay();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttach);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MobThresholdDamagedEvent>(OnThresholdCheck);
    }

    public void OnStateEntered(GameplayState state)
    {
        _overlayManager.AddOverlay(_overlay);
    }

    public void OnStateExited(GameplayState state)
    {
        _overlayManager.RemoveOverlay(_overlay);
    }

    private void OnPlayerAttach(PlayerAttachedEvent args)
    {
        ClearOverlay();
        if (!EntityManager.TryGetComponent<MobStateComponent>(args.Entity, out var mobState))
            return;
        if (mobState.CurrentState != MobState.Dead)
            UpdateOverlays(args.Entity, mobState);
        _overlayManager.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        _overlayManager.RemoveOverlay(_overlay);
        ClearOverlay();
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.Entity != _playerManager.LocalPlayer?.ControlledEntity)
            return;

        UpdateOverlays(args.Entity, args.Component);
    }

    private void OnThresholdCheck(MobThresholdDamagedEvent args)
    {

        if (args.Entity != _playerManager.LocalPlayer?.ControlledEntity)
            return;
        UpdateOverlays(args.Entity, args.MobStateComp, args.DamageableComp, args.ThresholdComp);
    }

    private void ClearOverlay()
    {
        _overlay.DeadLevel = 0f;
        _overlay.CritLevel = 0f;
        _overlay.BruteLevel = 0f;
        _overlay.OxygenLevel = 0f;
    }

    //TODO: Jezi: adjust oxygen and hp overlays to use appropriate systems once bodysim is implemented
    private void UpdateOverlays(EntityUid entity, MobStateComponent? mobState, DamageableComponent? damageable = null, MobThresholdsComponent? thresholds = null)
    {
        if (mobState == null && !EntityManager.TryGetComponent(entity, out mobState) ||
            thresholds == null && !EntityManager.TryGetComponent(entity, out thresholds) ||
            damageable == null && !EntityManager.TryGetComponent(entity, out  damageable))
            return;


        if (!_mobThresholdSystem.TryGetIncapThreshold(entity, out var foundThreshold, thresholds))
            return; //this entity cannot die or crit!!
        var critThreshold = foundThreshold.Value;
        _overlay.State = mobState.CurrentState;

        switch (mobState.CurrentState)
        {
            case MobState.Alive:
            {
                if (damageable.DamagePerGroup.TryGetValue("Brute", out var bruteDamage))
                {
                    _overlay.BruteLevel = FixedPoint2.Min(1f, bruteDamage / critThreshold).Float();
                }

                if (damageable.DamagePerGroup.TryGetValue("Airloss", out var oxyDamage))
                {
                    _overlay.OxygenLevel = FixedPoint2.Min(1f, oxyDamage / critThreshold).Float();
                }

                if (_overlay.BruteLevel < 0.05f) // Don't show damage overlay if they're near enough to max.
                {
                    _overlay.BruteLevel = 0;
                }
                break;
            }
            case MobState.Critical:
            {
                if (!_mobThresholdSystem.TryGetDeadPercentage(entity,
                        FixedPoint2.Max(0.0, damageable.TotalDamage), out var critLevel))
                    return;
                _overlay.CritLevel = critLevel.Value.Float();
                break;
            }
            case MobState.Dead:
            {
                break;
            }
        }
    }
}
