using System;
using System.Linq;
using System.Threading.Tasks;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Damageable
{
    [TestFixture]
    [TestOf(typeof(DamageableComponent))]
    public class DamageableTest : ContentIntegrationTest
    {
        private const string DamageableEntityId = "DamageableEntityId";
        private static readonly string Prototypes = $@"
- type: entity
  id: {DamageableEntityId}
  name: {DamageableEntityId}
  components:
  - type: Damageable
    damageContainer: allDamageContainer";

        [Test]
        public async Task TestDamageTypeDamageAndHeal()
        {
            var server = StartServerDummyTicker(new ServerContentIntegrationOption
            {
                ExtraPrototypes = Prototypes
            });

            await server.WaitIdleAsync();

            var sEntityManager = server.ResolveDependency<IEntityManager>();
            var sMapManager = server.ResolveDependency<IMapManager>();
            var sPrototypeManager = server.ResolveDependency<IPrototypeManager>();

            IEntity sDamageableEntity;
            IDamageableComponent sDamageableComponent = null;

            await server.WaitPost(() =>
            {
                var mapId = sMapManager.NextMapId();
                var coordinates = new MapCoordinates(0, 0, mapId);
                sMapManager.CreateMap(mapId);

                sDamageableEntity = sEntityManager.SpawnEntity(DamageableEntityId, coordinates);
                sDamageableComponent = sDamageableEntity.GetComponent<IDamageableComponent>();
            });

            await server.WaitRunTicks(5);

            await server.WaitAssertion(() =>
            {
                Assert.That(sDamageableComponent.TotalDamage, Is.EqualTo(0));

                var damageToDeal = 7;

                foreach (var damageType in sPrototypeManager.EnumeratePrototypes<DamageTypePrototype>())
                {
                    Assert.That(sDamageableComponent.SupportsDamageType(damageType));

                    // Damage
                    Assert.That(sDamageableComponent.ChangeDamage(damageType, damageToDeal, true), Is.True);
                    Assert.That(sDamageableComponent.TotalDamage, Is.EqualTo(damageToDeal));
                    Assert.That(sDamageableComponent.TryGetDamage(damageType, out var damage), Is.True);
                    Assert.That(damage, Is.EqualTo(damageToDeal));

                    // Heal
                    Assert.That(sDamageableComponent.ChangeDamage(damageType, -damageToDeal, true), Is.True);
                    Assert.That(sDamageableComponent.TotalDamage, Is.Zero);
                    Assert.That(sDamageableComponent.TryGetDamage(damageType, out damage), Is.True);
                    Assert.That(damage, Is.Zero);
                }
            });
        }

        [Test]
        public async Task TestDamageGroupDamageAndHeal()
        {
            var server = StartServerDummyTicker(new ServerContentIntegrationOption
            {
                ExtraPrototypes = Prototypes
            });

            await server.WaitIdleAsync();

            var sEntityManager = server.ResolveDependency<IEntityManager>();
            var sMapManager = server.ResolveDependency<IMapManager>();
            var sPrototypeManager = server.ResolveDependency<IPrototypeManager>();

            IEntity sDamageableEntity;
            IDamageableComponent sDamageableComponent = null;

            await server.WaitPost(() =>
            {
                var mapId = sMapManager.NextMapId();
                var coordinates = new MapCoordinates(0, 0, mapId);
                sMapManager.CreateMap(mapId);

                sDamageableEntity = sEntityManager.SpawnEntity(DamageableEntityId, coordinates);
                sDamageableComponent = sDamageableEntity.GetComponent<IDamageableComponent>();
            });

            await server.WaitRunTicks(5);

            await server.WaitAssertion(() =>
            {
                Assert.That(sDamageableComponent.TotalDamage, Is.EqualTo(0));

                foreach (var damageGroup in sPrototypeManager.EnumeratePrototypes<DamageGroupPrototype>())
                {
                    Assert.That(sDamageableComponent.SupportsDamageGroup(damageGroup));

                    var types = damageGroup.DamageTypes;

                    foreach (var type in types)
                    {
                        Assert.That(sDamageableComponent.SupportsDamageType(type));
                    }

                    var damageToDeal = types.Count() * 5;

                    // Damage
                    Assert.That(sDamageableComponent.ChangeDamage(damageGroup, damageToDeal, true), Is.True);
                    Assert.That(sDamageableComponent.TotalDamage, Is.EqualTo(damageToDeal));
                    Assert.That(sDamageableComponent.TryGetDamage(damageGroup, out var classDamage), Is.True);
                    Assert.That(classDamage, Is.EqualTo(damageToDeal));

                    foreach (var type in types)
                    {
                        Assert.That(sDamageableComponent.TryGetDamage(type, out var typeDamage), Is.True);
                        Assert.That(typeDamage, Is.EqualTo(damageToDeal / types.Count()));
                    }

                    // Heal
                    Assert.That(sDamageableComponent.ChangeDamage(damageGroup, -damageToDeal, true), Is.True);
                    Assert.That(sDamageableComponent.TotalDamage, Is.Zero);
                    Assert.That(sDamageableComponent.TryGetDamage(damageGroup, out classDamage), Is.True);
                    Assert.That(classDamage, Is.Zero);

                    foreach (var type in types)
                    {
                        Assert.That(sDamageableComponent.TryGetDamage(type, out var typeDamage), Is.True);
                        Assert.That(typeDamage, Is.Zero);
                    }
                }
            });
        }

        [Test]
        public async Task TotalDamageTest()
        {
            var server = StartServerDummyTicker(new ServerContentIntegrationOption
            {
                ExtraPrototypes = Prototypes
            });

            await server.WaitIdleAsync();

            var sEntityManager = server.ResolveDependency<IEntityManager>();
            var sMapManager = server.ResolveDependency<IMapManager>();
            var sPrototypeManager = server.ResolveDependency<IPrototypeManager>();

            IEntity sDamageableEntity;
            IDamageableComponent sDamageableComponent = null;

            await server.WaitPost(() =>
            {
                var mapId = sMapManager.NextMapId();
                var coordinates = new MapCoordinates(0, 0, mapId);
                sMapManager.CreateMap(mapId);

                sDamageableEntity = sEntityManager.SpawnEntity(DamageableEntityId, coordinates);
                sDamageableComponent = sDamageableEntity.GetComponent<IDamageableComponent>();
            });

            await server.WaitAssertion(() =>
            {

                sPrototypeManager.TryIndex<DamageGroupPrototype>("Brute",out var damageGroup);
                var damage = 10;

                Assert.True(sDamageableComponent.ChangeDamage(damageGroup, damage, true));
                Assert.That(sDamageableComponent.TotalDamage, Is.EqualTo(10));

                var totalTypeDamage = 0;

                foreach (var damageType in sPrototypeManager.EnumeratePrototypes<DamageTypePrototype>())
                {
                    Assert.True(sDamageableComponent.TryGetDamage(damageType, out var typeDamage));
                    Assert.That(typeDamage, Is.LessThanOrEqualTo(damage));

                    totalTypeDamage += typeDamage;
                }

                Assert.That(totalTypeDamage, Is.EqualTo(damage));
            });
        }
    }
}
