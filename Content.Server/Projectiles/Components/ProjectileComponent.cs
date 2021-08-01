using System.Collections.Generic;
using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Players;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Server.Projectiles.Components
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedProjectileComponent))]
    public class ProjectileComponent : SharedProjectileComponent
    {

        // TODO PROTOTYPE Replace this datafield variable with prototype references, once they are supported.
        // This also requires changing the dictionary type and modifying ProjectileSystem.cs, which uses it.
        // While thats being done, also replace "damages" -> "damageTypes" For consistency.
        [DataField("damages")]
        private Dictionary<string, int> _damageTypes = new();

        [ViewVariables]
        public Dictionary<string, int> Damages
        {
            get => _damageTypes;
            set => _damageTypes = value;
        }

        [DataField("deleteOnCollide")]
        public bool DeleteOnCollide { get; } = true;

        // Get that juicy FPS hit sound
        [DataField("soundHit")] public string? SoundHit = default;
        [DataField("soundHitSpecies")] public string? SoundHitSpecies = default;

        public bool DamagedEntity;

        public float TimeLeft { get; set; } = 10;

        /// <summary>
        /// Function that makes the collision of this object ignore a specific entity so we don't collide with ourselves
        /// </summary>
        /// <param name="shooter"></param>
        public void IgnoreEntity(IEntity shooter)
        {
            Shooter = shooter.Uid;
            Dirty();
        }

        public override ComponentState GetComponentState(ICommonSession player)
        {
            return new ProjectileComponentState(Shooter, IgnoreShooter);
        }
    }
}
