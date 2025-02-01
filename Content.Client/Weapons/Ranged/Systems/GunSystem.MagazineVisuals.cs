﻿using Content.Client.Weapons.Ranged.Components;
using Content.Shared.Rounding;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Client.GameObjects;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    
    private void InitializeMagazineVisuals()
    {
        SubscribeLocalEvent<MagazineVisualsComponent, ComponentInit>(OnMagazineVisualsInit);
        SubscribeLocalEvent<MagazineVisualsComponent, AppearanceChangeEvent>(OnMagazineVisualsChange);
    }
    
    public void SetMagState(EntityUid uid, string? magState, bool force = false, MagazineVisualsComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (!force && component.MagState == magState)
            return;

        component.MagState = magState;
        
        if (TryComp<AppearanceComponent>(uid, out var appearance))
            _appearance.QueueUpdate(uid, appearance);
    }

    private void OnMagazineVisualsInit(EntityUid uid, MagazineVisualsComponent component, ComponentInit args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite)) return;

        if (sprite.LayerMapTryGet(GunVisualLayers.Mag, out _))
        {
            sprite.LayerSetState(GunVisualLayers.Mag, $"{component.MagState}-{component.MagSteps - 1}");
            sprite.LayerSetVisible(GunVisualLayers.Mag, false);
        }

        if (sprite.LayerMapTryGet(GunVisualLayers.Tip, out _)) //🌟Starlight🌟
        {
            sprite.LayerSetState(GunVisualLayers.Tip, $"{component.MagState}-tip-{component.MagSteps - 1}");
            sprite.LayerSetVisible(GunVisualLayers.Tip, false);
        }

        if (sprite.LayerMapTryGet(GunVisualLayers.MagUnshaded, out _))
        {
            sprite.LayerSetState(GunVisualLayers.MagUnshaded, $"{component.MagState}-unshaded-{component.MagSteps - 1}");
            sprite.LayerSetVisible(GunVisualLayers.MagUnshaded, false);
        }
    }

    private void OnMagazineVisualsChange(EntityUid uid, MagazineVisualsComponent component, ref AppearanceChangeEvent args)
    {
        // tl;dr
        // 1.If no mag then hide it OR
        // 2. If step 0 isn't visible then hide it (mag or unshaded)
        // 3. Otherwise just do mag / unshaded as is
        var sprite = args.Sprite;

        if (sprite == null) return;

        if (!args.AppearanceData.TryGetValue(AmmoVisuals.MagLoaded, out var magloaded) ||
            magloaded is true)
        {
            if (!args.AppearanceData.TryGetValue(AmmoVisuals.AmmoMax, out var capacity))
            {
                capacity = component.MagSteps;
            }

            if (!args.AppearanceData.TryGetValue(AmmoVisuals.AmmoCount, out var current))
            {
                current = component.MagSteps;
            }

            var step = ContentHelpers.RoundToLevels((int) current, (int) capacity, component.MagSteps);

            if (step == 0 && !component.ZeroVisible)
            {
                if (sprite.LayerMapTryGet(GunVisualLayers.Mag, out _))
                {
                    sprite.LayerSetVisible(GunVisualLayers.Mag, false);
                }

                if (sprite.LayerMapTryGet(GunVisualLayers.MagUnshaded, out _))
                {
                    sprite.LayerSetVisible(GunVisualLayers.MagUnshaded, false);
                }

                return;
            }

            if (sprite.LayerMapTryGet(GunVisualLayers.Mag, out _))
            {
                sprite.LayerSetVisible(GunVisualLayers.Mag, true);
                sprite.LayerSetState(GunVisualLayers.Mag, $"{component.MagState}-{step}");
            }

            if (sprite.LayerMapTryGet(GunVisualLayers.MagUnshaded, out _))
            {
                sprite.LayerSetVisible(GunVisualLayers.MagUnshaded, true);
                sprite.LayerSetState(GunVisualLayers.MagUnshaded, $"{component.MagState}-unshaded-{step}");
            }

            if (sprite.LayerMapTryGet(GunVisualLayers.Tip, out _)) //🌟Starlight🌟
            {
                sprite.LayerSetVisible(GunVisualLayers.Tip, true);
                sprite.LayerSetState(GunVisualLayers.Tip, $"{component.MagState}-tip-{step}");
            }
        }
        else
        {
            if (sprite.LayerMapTryGet(GunVisualLayers.Mag, out _))
            {
                sprite.LayerSetVisible(GunVisualLayers.Mag, false);
            }

            if (sprite.LayerMapTryGet(GunVisualLayers.MagUnshaded, out _))
            {
                sprite.LayerSetVisible(GunVisualLayers.MagUnshaded, false);
            }

            if (sprite.LayerMapTryGet(GunVisualLayers.Tip, out _)) //🌟Starlight🌟
            {
                sprite.LayerSetVisible(GunVisualLayers.Tip, false);
            }
        }
    }
}
