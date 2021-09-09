using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using System.Collections.Generic;

namespace Content.Tests.Shared
{
    // Basic tests of various damage prototypes and classes.
    [TestFixture]
    [TestOf(typeof(DamageSpecifier))]
    [TestOf(typeof(ResistanceSetPrototype))]
    [TestOf(typeof(DamageGroupPrototype))]
    public class DamageTest : ContentUnitTest
    {

        static private Dictionary<string, float> _resistanceCoefficientDict = new()
        {
            // "missing" blunt entry
            { "Piercing", -2 },// Turn Piercing into Healing
            { "Slash", 3 },
            { "Radiation", 1.06f }, // Small change, paired with fractional reduction
        };

        static private Dictionary<string, float> _resistanceReductionDict = new()
        {
            { "Blunt", - 5 }, 
            // "missing" piercing entry
            { "Slash", 8 },
            { "Radiation", 0.5f },  // Fractional adjustment
        };

        private IPrototypeManager _prototypeManager;

        private DamageSpecifier _damageSpec;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            IoCManager.Resolve<ISerializationManager>().Initialize();
            _prototypeManager = IoCManager.Resolve<IPrototypeManager>();
            _prototypeManager.Initialize();
            _prototypeManager.LoadString(_damagePrototypes);
            _prototypeManager.Resync();

            // Create a damage data set
            _damageSpec = new(_prototypeManager.Index<DamageGroupPrototype>("Brute"), 6);
            _damageSpec += new DamageSpecifier(_prototypeManager.Index<DamageTypePrototype>("Radiation"), 3);
            _damageSpec += new DamageSpecifier(_prototypeManager.Index<DamageTypePrototype>("Slash"), -1); // already exists in brute
        }

        //Check that DamageSpecifier will split groups and can do arithmetic operations
        [Test]
        public void DamageSpecifierTest()
        {
            // Create a copy of the damage data
            DamageSpecifier damageSpec = new(_damageSpec);

            // Check that it properly split up the groups into types
            int damage;
            Assert.That(damageSpec.Total, Is.EqualTo(8));
            Assert.That(damageSpec.DamageDict.TryGetValue("Blunt", out damage));
            Assert.That(damage, Is.EqualTo(2));
            Assert.That(damageSpec.DamageDict.TryGetValue("Piercing", out damage));
            Assert.That(damage, Is.EqualTo(2));
            Assert.That(damageSpec.DamageDict.TryGetValue("Slash", out damage));
            Assert.That(damage, Is.EqualTo(1));
            Assert.That(damageSpec.DamageDict.TryGetValue("Radiation", out damage));
            Assert.That(damage, Is.EqualTo(3));

            // check that integer multiplication works
            damageSpec = damageSpec * 2;
            Assert.That(damageSpec.Total, Is.EqualTo(16));
            Assert.That(damageSpec.DamageDict.TryGetValue("Blunt", out damage));
            Assert.That(damage, Is.EqualTo(4));
            Assert.That(damageSpec.DamageDict.TryGetValue("Piercing", out damage));
            Assert.That(damage, Is.EqualTo(4));
            Assert.That(damageSpec.DamageDict.TryGetValue("Slash", out damage));
            Assert.That(damage, Is.EqualTo(2));
            Assert.That(damageSpec.DamageDict.TryGetValue("Radiation", out damage));
            Assert.That(damage, Is.EqualTo(6));

            // check that float multiplication works
            damageSpec = damageSpec * 2.2f;
            Assert.That(damageSpec.DamageDict.TryGetValue("Blunt", out damage));
            Assert.That(damage, Is.EqualTo(9));
            Assert.That(damageSpec.DamageDict.TryGetValue("Piercing", out damage));
            Assert.That(damage, Is.EqualTo(9));
            Assert.That(damageSpec.DamageDict.TryGetValue("Slash", out damage));
            Assert.That(damage, Is.EqualTo(4));
            Assert.That(damageSpec.DamageDict.TryGetValue("Radiation", out damage));
            Assert.That(damage, Is.EqualTo(13));
            Assert.That(damageSpec.Total, Is.EqualTo(9 + 9 + 4 + 13));

            // check that integer division works
            damageSpec = damageSpec / 2;
            Assert.That(damageSpec.DamageDict.TryGetValue("Blunt", out damage));
            Assert.That(damage, Is.EqualTo(5));
            Assert.That(damageSpec.DamageDict.TryGetValue("Piercing", out damage));
            Assert.That(damage, Is.EqualTo(5));
            Assert.That(damageSpec.DamageDict.TryGetValue("Slash", out damage));
            Assert.That(damage, Is.EqualTo(2));
            Assert.That(damageSpec.DamageDict.TryGetValue("Radiation", out damage));
            Assert.That(damage, Is.EqualTo(7));

            // check that float division works
            damageSpec = damageSpec / 2.4f;
            Assert.That(damageSpec.DamageDict.TryGetValue("Blunt", out damage));
            Assert.That(damage, Is.EqualTo(2));
            Assert.That(damageSpec.DamageDict.TryGetValue("Piercing", out damage));
            Assert.That(damage, Is.EqualTo(2));
            Assert.That(damageSpec.DamageDict.TryGetValue("Slash", out damage));
            Assert.That(damage, Is.EqualTo(1));
            Assert.That(damageSpec.DamageDict.TryGetValue("Radiation", out damage));
            Assert.That(damage, Is.EqualTo(3));

            // Lets also test the constructor with damage types and damage groups works properly.
            damageSpec = new(_prototypeManager.Index<DamageGroupPrototype>("Brute"), 4);
            Assert.That(damageSpec.DamageDict.TryGetValue("Blunt", out damage));
            Assert.That(damage, Is.EqualTo(1));
            Assert.That(damageSpec.DamageDict.TryGetValue("Piercing", out damage));
            Assert.That(damage, Is.EqualTo(2)); // integer rounding. Piercing is defined as last group member in yaml.
            Assert.That(damageSpec.DamageDict.TryGetValue("Slash", out damage));
            Assert.That(damage, Is.EqualTo(1));

            damageSpec = new(_prototypeManager.Index<DamageTypePrototype>("Piercing"), 4);
            Assert.That(damageSpec.DamageDict.TryGetValue("Piercing", out damage));
            Assert.That(damage, Is.EqualTo(4));
        }

        //Check that DamageSpecifier will be properly adjusted by a resistance set
        [Test]
        public void ResistanceSetTest()
        {
            // Create a copy of the damage data
            DamageSpecifier damageSpec = 10 * new DamageSpecifier(_damageSpec);

            // Create a resistance set
            ResistanceSetPrototype resistanceSet = new()
            {
                Coefficients = _resistanceCoefficientDict,
                FlatReduction = _resistanceReductionDict
            };

            //damage is initially   20 / 20 / 10 / 30
            //Each time we subtract -5 /  0 /  8 /  0.5
            //then multiply by       1 / -2 /  3 /  1.06

            // Apply once
            damageSpec = DamageSpecifier.ApplyResistanceSet(damageSpec, resistanceSet);
            Assert.That(damageSpec.DamageDict["Blunt"], Is.EqualTo(25));
            Assert.That(damageSpec.DamageDict["Piercing"], Is.EqualTo(-40)); // became healing
            Assert.That(damageSpec.DamageDict["Slash"], Is.EqualTo(6));
            Assert.That(damageSpec.DamageDict["Radiation"], Is.EqualTo(31)); // would be 32 w/o fraction adjustment

            // And again, checking for some other behavior
            damageSpec = DamageSpecifier.ApplyResistanceSet(damageSpec, resistanceSet);
            Assert.That(damageSpec.DamageDict["Blunt"], Is.EqualTo(30));
            Assert.That(damageSpec.DamageDict["Piercing"], Is.EqualTo(-40)); // resistances don't apply to healing
            Assert.That(!damageSpec.DamageDict.ContainsKey("Slash"));  // Reduction reduced to 0, and removed from specifier
            Assert.That(damageSpec.DamageDict["Radiation"], Is.EqualTo(32));
        }

        // Default damage Yaml
        private string _damagePrototypes = @"
- type: damageType
  id: Blunt

- type: damageType
  id: Slash

- type: damageType
  id: Piercing

- type: damageType
  id: Heat

- type: damageType
  id: Shock

- type: damageType
  id: Cold

# Poison damage. Generally caused by various reagents being metabolised.
- type: damageType
  id: Poison

- type: damageType
  id: Radiation

# Damage due to being unable to breathe.
# Represents not enough oxygen (or equivalent) getting to the blood.
# Usually healed automatically if entity can breathe
- type: damageType
  id: Asphyxiation

# Damage representing not having enough blood.
# Represents there not enough blood to supply oxygen (or equivalent).
- type: damageType
  id: Bloodloss

- type: damageType
  id: Cellular

- type: damageGroup
  id: Brute
  damageTypes:
    - Blunt
    - Slash
    - Piercing

- type: damageGroup
  id: Burn
  damageTypes:
    - Heat
    - Shock
    - Cold

# Airloss (sometimes called oxyloss)
# Caused by asphyxiation or bloodloss.
# Note that most medicine and damaging effects should probably modify either asphyxiation or
# bloodloss, not this whole group, unless you have a wonder drug that affects both.
- type: damageGroup
  id: Airloss
  damageTypes:
    - Asphyxiation
    - Bloodloss

# As with airloss, most medicine and damage effects should probably modify either poison or radiation.
# Though there are probably some radioactive poisons.
- type: damageGroup
  id: Toxin
  damageTypes:
    - Poison
    - Radiation

- type: damageGroup
  id: Genetic
  damageTypes:
    - Cellular

- type: resistanceSet
  id: Metallic
  coefficients:
    Blunt: 0.7
    Slash: 0.5
    Piercing: 0.7
    Shock: 1.2
  flatReductions:
    Blunt: 5

- type: resistanceSet
  id: Inflatable
  coefficients:
    Blunt: 0.5
    Piercing: 2.0
    Heat: 0.5
    Shock: 0
  flatReductions:
    Blunt: 5

- type: resistanceSet
  id: Glass
  coefficients:
    Blunt: 0.5
    Slash: 0.5
    Piercing: 0.5
    Heat: 0
    Shock: 0
  flatReductions:
    Blunt: 5

- type: damageContainer
  id: Biological
  supportedGroups:
    - Brute
    - Burn
    - Toxin
    - Airloss
    - Genetic

- type: damageContainer
  id: Inorganic
  supportedGroups:
    - Brute
  supportedTypes:
    - Heat
    - Shock
";
    }
}
