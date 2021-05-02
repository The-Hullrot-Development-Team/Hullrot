﻿using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.GameObjects.Components.Strap;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Client.GameObjects.Components.Strap
{
    /// <summary>
    /// This manages an object with a <see cref="StrapComponent"/> visuals.
    /// You specify a buckledSuffix in the prototype, and the visualizer will look for
    /// [state]_buckledSuffix state for every layer in the sprite.
    /// If it doesn't find it, it leaves the state as-is.
    ///
    /// By default this suffix is _buckled.
    /// </summary>
    [UsedImplicitly]
    public class StrapVisualizer : AppearanceVisualizer
    {
        /// <summary>
        /// The suffix used to define the buckled states names in RSI file.
        /// [state] becomes [state][suffix],
        /// default suffix is _buckled, so chair becomes chair_buckled
        /// </summary>
        [ViewVariables][DataField("buckledSuffix", required: false)]
        private string? _buckledSuffix = "_buckled";

        // This array keeps the original layers' states of the sprite
        private string?[]? _defaultStates;

        // This keeps a reference to the entity's SpriteComponent to avoid fetching it each time
        private ISpriteComponent? _sprite;

        private bool? _isBuckled;


        public override void OnChangeData(AppearanceComponent appearance)
        {
            base.OnChangeData(appearance);

            // Do nothing if the object doesn't have a SpriteComponent
            if (_sprite == null)
                if (appearance.Owner.TryGetComponent(out ISpriteComponent? s))
                {
                    this._sprite = s;    // Keep a reference to the sprite for good measure
                } else return;

            // Get appearance data, and check if the buckled state has changed
            if (!appearance.TryGetData(StrapVisuals.BuckledState, out bool folded)) return;
            if (_isBuckled != null && _isBuckled == folded) return;

            // Set all the layers state to [state]_buckled if it exists, or back to the default [state]

            _isBuckled = folded;

            var i = 0;
            foreach (var layer in _sprite.AllLayers)
            {
                var newState = folded
                    ? _sprite.LayerGetState(i) + _buckledSuffix
                    : _sprite.LayerGetState(i).ToString()?.Replace(_buckledSuffix ?? "", "");

                // Check if the state actually exists in the RSI and set it on the layer
                var stateExists = _sprite.BaseRSI?.TryGetState(newState, out var actualState);
                if (stateExists ?? false)
                {
                    _sprite.LayerSetState(i, newState);
                }

                i++;
            }

        }
    }
}
