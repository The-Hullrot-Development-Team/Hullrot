﻿using Content.Server.GameObjects.Components.Effects;
using Content.Server.GameObjects.Components.Kitchen;
using JetBrains.Annotations;
using Robust.Shared.GameObjects.Systems;

namespace Content.Server.GameObjects.EntitySystems
{
    [UsedImplicitly]
    internal sealed class BaseAuraTickSystem : EntitySystem
    {
        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            foreach (var shaderAura in ComponentManager.EntityQuery<BaseShaderAuraComponent>())
            {
                shaderAura.OnTick();
            }
        }
    }
}
