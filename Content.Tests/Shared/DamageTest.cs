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
    [TestOf(typeof(DamageData))]
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

        private DamageData _data;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            IoCManager.Resolve<ISerializationManager>().Initialize();
            _prototypeManager = IoCManager.Resolve<IPrototypeManager>();
            _prototypeManager.LoadString(_damagePrototypes);
            _prototypeManager.Resync();

            // Create a damage data set
            _data = new(_prototypeManager.Index<DamageGroupPrototype>("Brute"), 6);
            _data += new DamageData(_prototypeManager.Index<DamageTypePrototype>("Radiation"), 3);
            _data += new DamageData(_prototypeManager.Index<DamageTypePrototype>("Slash"), -1); // already exists in brute
        }

        //Check that DamageData will split groups and can do arithmetic operations
        [Test]
        public void DamageDataTest()
        {
            // Create a copy of the damage data
            DamageData data = new(_data);

            // Check that it properly split up the groups into types
            int damage;
            Assert.That(data.TotalDamage, Is.EqualTo(8));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Blunt"), out damage));
            Assert.That(damage, Is.EqualTo(2));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Piercing"), out damage));
            Assert.That(damage, Is.EqualTo(2));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Slash"), out damage));
            Assert.That(damage, Is.EqualTo(1));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Radiation"), out damage));
            Assert.That(damage, Is.EqualTo(3));

            // check that integer multiplication works
            data = data * 2;
            Assert.That(data.TotalDamage, Is.EqualTo(16));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Blunt"), out damage));
            Assert.That(damage, Is.EqualTo(4));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Piercing"), out damage));
            Assert.That(damage, Is.EqualTo(4));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Slash"), out damage));
            Assert.That(damage, Is.EqualTo(2));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Radiation"), out damage));
            Assert.That(damage, Is.EqualTo(6));

            // check that float multiplication works
            data = data * 2.2f;
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Blunt"), out damage));
            Assert.That(damage, Is.EqualTo(9));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Piercing"), out damage));
            Assert.That(damage, Is.EqualTo(9));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Slash"), out damage));
            Assert.That(damage, Is.EqualTo(4));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Radiation"), out damage));
            Assert.That(damage, Is.EqualTo(13));
            Assert.That(data.TotalDamage, Is.EqualTo(9 + 9 + 4 + 13));

            // check that integer division works
            data = data / 2;
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Blunt"), out damage));
            Assert.That(damage, Is.EqualTo(5));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Piercing"), out damage));
            Assert.That(damage, Is.EqualTo(5));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Slash"), out damage));
            Assert.That(damage, Is.EqualTo(2));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Radiation"), out damage));
            Assert.That(damage, Is.EqualTo(7));

            // check that float division works
            data = data / 2.4f;
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Blunt"), out damage));
            Assert.That(damage, Is.EqualTo(2));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Piercing"), out damage));
            Assert.That(damage, Is.EqualTo(2));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Slash"), out damage));
            Assert.That(damage, Is.EqualTo(1));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Radiation"), out damage));
            Assert.That(damage, Is.EqualTo(3));

            // Lets also test the constructor with damage types and damage groups works properly.
            data = new(_prototypeManager.Index<DamageGroupPrototype>("Brute"), 4);
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Blunt"), out damage));
            Assert.That(damage, Is.EqualTo(1));
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Piercing"), out damage));
            Assert.That(damage, Is.EqualTo(2)); // integer rounding. Piercing is defined as last group member in yaml.
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Slash"), out damage));
            Assert.That(damage, Is.EqualTo(1));

            data = new(_prototypeManager.Index<DamageTypePrototype>("Piercing"), 4);
            Assert.That(data.DamageDict.TryGetValue(_prototypeManager.Index<DamageTypePrototype>("Piercing"), out damage));
            Assert.That(damage, Is.EqualTo(4));
        }

        //Check that DamageData will be properly adjusted by a resistance set
        [Test]
        public void ResistanceSetTest()
        {
            // Create a copy of the damage data
            DamageData data = 10 * new DamageData(_data);

            // Create a resistance set
            ResistanceSetPrototype resistanceSet = new();
            foreach (var damageTypeID in _resistanceCoefficientDict.Keys)
            {
                resistanceSet.Coefficients.Add(_prototypeManager.Index<DamageTypePrototype>(damageTypeID), _resistanceCoefficientDict[damageTypeID]);
            }
            foreach (var damageTypeID in _resistanceReductionDict.Keys)
            {
                resistanceSet.FlatReduction.Add(_prototypeManager.Index<DamageTypePrototype>(damageTypeID), _resistanceReductionDict[damageTypeID]);
            }

            //damage is initially   20 / 20 / 10 / 30
            //Each time we subtract -5 /  0 /  8 /  0.5
            //then multiply by       1 / -2 /  3 /  1.05

            // Apply once
            data = DamageData.ApplyResistanceSet(data, resistanceSet);
            Assert.That(data.DamageDict[_prototypeManager.Index<DamageTypePrototype>("Blunt")], Is.EqualTo(25));
            Assert.That(data.DamageDict[_prototypeManager.Index<DamageTypePrototype>("Piercing")], Is.EqualTo(-40)); // became healing
            Assert.That(data.DamageDict[_prototypeManager.Index<DamageTypePrototype>("Slash")], Is.EqualTo(6));
            Assert.That(data.DamageDict[_prototypeManager.Index<DamageTypePrototype>("Radiation")], Is.EqualTo(31)); // would be 32 w/o fraction adjustment

            // And again, checking for some other behavior
            data = DamageData.ApplyResistanceSet(data, resistanceSet);
            Assert.That(data.DamageDict[_prototypeManager.Index<DamageTypePrototype>("Blunt")], Is.EqualTo(30));
            Assert.That(data.DamageDict[_prototypeManager.Index<DamageTypePrototype>("Piercing")], Is.EqualTo(-40)); // resistances don't apply to healing
            Assert.That(data.DamageDict[_prototypeManager.Index<DamageTypePrototype>("Slash")], Is.EqualTo(0));  // Reduction reduced to 0.
            Assert.That(data.DamageDict[_prototypeManager.Index<DamageTypePrototype>("Radiation")], Is.EqualTo(32));
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
  id: AllDamage
  supportAll: true

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
