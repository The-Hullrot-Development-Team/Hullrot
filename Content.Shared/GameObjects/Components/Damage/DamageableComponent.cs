﻿using SS14.Shared.GameObjects;
using SS14.Shared.Serialization;
using System;
using System.Collections.Generic;

namespace Content.Shared.GameObjects
{
    public abstract class SharedDamageableComponent : Component
    {
        public override string Name => "Damageable";
        public sealed override uint? NetID => ContentNetIDs.DAMAGEABLE;
        public sealed override Type StateType => typeof(DamageComponentState);
    }

    // The IDs of the items get synced over the network.
    [Serializable, NetSerializable]
    public class DamageComponentState : ComponentState
    {
        public Dictionary<DamageType, int> CurrentDamage = new Dictionary<DamageType, int>();

        public DamageComponentState(Dictionary<DamageType, int> damage) : base(ContentNetIDs.DAMAGEABLE)
        {
            CurrentDamage = damage;
        }
    }

    /// <summary>
    /// Damage types used in-game.
    /// Total should never be used directly - it's a derived value.
    /// </summary>
    public enum DamageType
    {
        Total,
        Brute,
        Heat,
        Cold,
        Acid,
        Toxic,
        Electric
    }
}
