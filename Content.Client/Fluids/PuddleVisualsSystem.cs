﻿using System.Linq;
using Content.Shared.Fluids;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Shared.Random;

namespace Content.Client.Fluids
{
    [UsedImplicitly]
    public sealed class PuddleVisualsSystem : VisualizerSystem<PuddleVisualsComponent>
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<PuddleVisualsComponent, ComponentInit>(OnComponentInit);
        }

        private void OnComponentInit(EntityUid uid, PuddleVisualsComponent puddleVisuals, ComponentInit args)
        {
            if (!TryComp(uid, out AppearanceComponent? appearance))
            {
                Logger.Warning($"Missing AppearanceComponent for PuddleVisualsSystem on entityUid = {uid}");
                return;
            }

            if (!TryComp(uid, out SpriteComponent? sprite))
            {
                Logger.Warning($"Missing SpriteComponent for PuddleVisualsSystem on entityUid = {uid}");
                return;
            }

            if (!appearance.TryGetData(PuddleVisuals.VisualSeed, out float visualSeed))
            {
                Logger.Warning($"Missing VisualSeed for PuddleVisualsSystem on entityUid = {uid}");
                return;
            }

            var maxStates = sprite.BaseRSI?.ToArray();

            if (maxStates is not { Length: > 0 }) return;

            int intVisualSeed = ((int) (visualSeed * 1000000)); //uses the float seed to generate an arbitrarily large int seed. It is retyped to int for use in the Modulo function.

            int selectedState = intVisualSeed % maxStates.Length; // uses the visualSeed to randomly select an index for which RSI state to use. Modulo is used to get the remainder, so the value will always be between 0 and maxStates.Length.
            sprite.LayerSetState(PuddleVisualLayers.Puddle, maxStates[selectedState].StateId); // sets the sprite's state via our randomly selected index.

            int rotationDegrees = intVisualSeed % 360; // uses the visualSeed to randomly select a rotation for our puddle sprite.
            sprite.Rotation = Angle.FromDegrees(rotationDegrees); // sets the sprite's rotation to the one we randomly selected.


        }


        protected override void OnAppearanceChange(EntityUid uid, PuddleVisualsComponent component, ref AppearanceChangeEvent args)
        {
            if (TryComp(uid, out SpriteComponent? sprite)
                && args.Component.TryGetData(PuddleVisuals.VolumeScale, out float volumeScale)
                && args.Component.TryGetData(PuddleVisuals.SolutionColor, out Color solutionColor)
                && args.Component.TryGetData(PuddleVisuals.WetFloorEffect, out bool wetFloorEffect)
                )
            {

                // volumeScale is our opacity based on level of fullness to overflow. The lower bound is hard-capped for visibility reasons.
                var cappedScale = Math.Min(1.0f, volumeScale * 0.75f + 0.25f);

                Color newColor;
                if (component.Recolor)
                {
                    newColor = solutionColor.WithAlpha(cappedScale);
                }
                else
                {
                    newColor = sprite.Color.WithAlpha(cappedScale);
                }

                if (wetFloorEffect)
                {
                    // Hides the main puddle sprite layer
                    sprite.LayerSetVisible(PuddleVisualLayers.Puddle, false);

                    // Shows the wet floor sprite layers
                    sprite.LayerSetColor(PuddleVisualLayers.WetFloorEffect, newColor.WithAlpha(0.25f)); //Sparkles inherit the color of the puddle's solution, except they should be mostly transparent.
                    sprite.LayerSetVisible(PuddleVisualLayers.WetFloorEffect, true);
                }
                else
                {
                    // Hides the wet floor sprite layer
                    sprite.LayerSetVisible(PuddleVisualLayers.WetFloorEffect, false);

                    // Shows the main puddle sprite layer
                    sprite.LayerSetColor(PuddleVisualLayers.Puddle, newColor);
                    sprite.LayerSetVisible(PuddleVisualLayers.Puddle, true);
                }
            }
            else
            {
                Logger.Warning($"Missing SpriteComponent for PuddleVisualsSystem on entityUid = {uid}");
                return;
            }
        }
    }
}

public enum PuddleVisualLayers
{
    Puddle,
    WetFloorEffect
}
