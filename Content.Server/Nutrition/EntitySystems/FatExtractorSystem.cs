﻿using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Nutrition.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Storage.Components;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Content.Shared.Power;
using Content.Shared.Storage.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Nutrition.EntitySystems;

/// <summary>
/// This handles logic and interactions relating to <see cref="FatExtractorComponent"/>
/// </summary>
public sealed class FatExtractorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SatiationSystem _satiation = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly ProtoId<SatiationTypePrototype> HungerSatiation = "Hunger";

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<FatExtractorComponent, GotEmaggedEvent>(OnGotEmagged);
        SubscribeLocalEvent<FatExtractorComponent, StorageAfterCloseEvent>(OnClosed);
        SubscribeLocalEvent<FatExtractorComponent, StorageAfterOpenEvent>(OnOpen);
        SubscribeLocalEvent<FatExtractorComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnGotEmagged(EntityUid uid, FatExtractorComponent component, ref GotEmaggedEvent args)
    {
        args.Handled = true;
        args.Repeatable = false;
    }

    private void OnClosed(EntityUid uid, FatExtractorComponent component, ref StorageAfterCloseEvent args)
    {
        StartProcessing(uid, component);
    }

    private void OnOpen(EntityUid uid, FatExtractorComponent component, ref StorageAfterOpenEvent args)
    {
        StopProcessing(uid, component);
    }

    private void OnPowerChanged(EntityUid uid, FatExtractorComponent component, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            StopProcessing(uid, component);
    }

    public void StartProcessing(EntityUid uid, FatExtractorComponent? component = null, EntityStorageComponent? storage = null)
    {
        if (!Resolve(uid, ref component, ref storage))
            return;

        if (component.Processing)
            return;

        if (!this.IsPowered(uid, EntityManager))
            return;

        if (!TryGetValidOccupant(uid, out _, component, storage))
            return;

        component.Processing = true;
        _appearance.SetData(uid, FatExtractorVisuals.Processing, true);
        component.Stream = _audio.PlayPvs(component.ProcessSound, uid)?.Entity;
        component.NextUpdate = _timing.CurTime + component.UpdateTime;
    }

    public void StopProcessing(EntityUid uid, FatExtractorComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!component.Processing)
            return;

        component.Processing = false;
        _appearance.SetData(uid, FatExtractorVisuals.Processing, false);
        component.Stream = _audio.Stop(component.Stream);
    }

    public bool TryGetValidOccupant(EntityUid uid,
        [NotNullWhen(true)] out Entity<SatiationComponent>? occupant,
        FatExtractorComponent? component = null,
        EntityStorageComponent? storage = null)
    {
        occupant = null;
        if (!Resolve(uid, ref component, ref storage))
            return false;

        var firstEntity = storage.Contents.ContainedEntities.FirstOrDefault();
        if (firstEntity is not { Valid: true })
        {
            return false;
        }

        if (!TryComp<SatiationComponent>(firstEntity, out var satiation))
            return false;

        Entity<SatiationComponent> entity = (firstEntity, satiation);

        if (_satiation.GetValueOrNull(entity, HungerSatiation) < component.NutritionPerSecond)
            return false;

        if (_satiation.GetThresholdOrNull(entity, HungerSatiation) < component.MinHungerThreshold &&
            !HasComp<EmaggedComponent>(uid))
            return false;

        occupant = entity;
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FatExtractorComponent, EntityStorageComponent>();
        while (query.MoveNext(out var uid, out var fat, out var storage))
        {
            if (TryGetValidOccupant(uid, out var occupant, fat, storage))
            {
                if (!fat.Processing)
                    StartProcessing(uid, fat, storage);
            }
            else
            {
                StopProcessing(uid, fat);
                continue;
            }

            if (!fat.Processing)
                continue;

            if (_timing.CurTime < fat.NextUpdate)
                continue;
            fat.NextUpdate += fat.UpdateTime;

            _satiation.ModifyValue(occupant.Value, HungerSatiation, -fat.NutritionPerSecond);
            fat.NutrientAccumulator += fat.NutritionPerSecond;
            if (fat.NutrientAccumulator >= fat.NutrientPerMeat)
            {
                fat.NutrientAccumulator -= fat.NutrientPerMeat;
                Spawn(fat.MeatPrototype, Transform(uid).Coordinates);
            }
        }
    }
}
