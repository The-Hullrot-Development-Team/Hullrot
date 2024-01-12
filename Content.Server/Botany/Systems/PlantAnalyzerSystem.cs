using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Botany.Components;
using Content.Server.PowerCell;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.PlantAnalyzer;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;

namespace Content.Server.Botany.Systems;

public sealed class PlantAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private PlantHolderComponent _plantHolder = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlantAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<PlantAnalyzerComponent, PlantAnalyzerDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(EntityUid uid, PlantAnalyzerComponent plantAnalyzer, AfterInteractEvent args)
    {
        if (args.Target == null || !args.CanReach || !HasComp<SeedComponent>(args.Target) && !HasComp<PlantHolderComponent>(args.Target) || !_cell.HasActivatableCharge(uid, user: args.User))
            return;
        _audio.PlayPvs(plantAnalyzer.ScanningBeginSound, uid);

        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, plantAnalyzer.ScanDelay, new PlantAnalyzerDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            BreakOnTargetMove = true,
            BreakOnUserMove = true,
            NeedHand = true
        });
    }

    private void OnDoAfter(EntityUid uid, PlantAnalyzerComponent component, DoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null || !_cell.TryUseActivatableCharge(uid, user: args.User))
            return;

        _audio.PlayPvs(component.ScanningEndSound, args.Args.User);

        UpdateScannedUser(uid, args.Args.User, args.Args.Target.Value, component);
        args.Handled = true;
    }

    private void OpenUserInterface(EntityUid user, EntityUid analyzer)
    {
        if (!TryComp<ActorComponent>(user, out var actor) || !_uiSystem.TryGetUi(analyzer, PlantAnalyzerUiKey.Key, out var ui))
            return;
        _uiSystem.OpenUi(ui, actor.PlayerSession);
    }

    public void UpdateScannedUser(EntityUid uid, EntityUid user, EntityUid target, PlantAnalyzerComponent? plantAnalyzer)
    {
        if (!Resolve(uid, ref plantAnalyzer))
            return;
        if (target == null || !_uiSystem.TryGetUi(uid, PlantAnalyzerUiKey.Key, out var ui))
            return;

        TryComp<PlantHolderComponent>(target, out var plantcomp);
        TryComp<SeedComponent>(target, out var seedcomponent);

        var state = default(PlantAnalyzerScannedSeedPlantInformation);
        var seedData = default(SeedData);   // data of a unique Seed
        var seedProtoId = default(SeedPrototype);   // data of base seed prototype 

        if (seedcomponent != null)
        {
            if (seedcomponent.Seed != null) //if unqiue seed
            {
                seedData = seedcomponent.Seed;
                state = ObtainingGeneDataSeed(seedData, target, false);
            }
            else if (seedcomponent.SeedId != null && _prototypeManager.TryIndex(seedcomponent.SeedId, out SeedPrototype? protoSeed)) // getting the protoype seed
            {
                seedProtoId = protoSeed;
                state = ObtainingGeneDataSeedProt(protoSeed, target);
            }
        }
        else if (plantcomp != null)    //where check if we poke the plantholder, it checks the plantholder seed
        {
            _plantHolder = plantcomp;
            seedData = plantcomp.Seed;
            if (seedData != null)
            {
                state = ObtainingGeneDataSeed(seedData, target, true); //seedData is a unique seed in a tray
            }
        }

        if (state == null)
            return;
        OpenUserInterface(user, uid);
        _uiSystem.SendUiMessage(ui, state);
    }

    /// <summary>
    ///     Analysis of seed from prototype.
    /// </summary>
    public PlantAnalyzerScannedSeedPlantInformation ObtainingGeneDataSeedProt(SeedPrototype comp, EntityUid target)
    {
        var seedName = Loc.GetString(comp.DisplayName);
        var seedYield = comp.Yield;
        var seedPotency = comp.Potency;
        var seedChem = "\r\n   ";
        var plantHarvestType = "";
        var plantMutations = "";
        var exudeGases = "";

        var plantSpeciation = "";

        if (comp.HarvestRepeat == HarvestType.Repeat) plantHarvestType = Loc.GetString("plant-analyzer-harvest-repeat");
        if (comp.HarvestRepeat == HarvestType.NoRepeat) plantHarvestType = Loc.GetString("plant-analyzer-harvest-ephemeral");
        if (comp.HarvestRepeat == HarvestType.SelfHarvest) plantHarvestType = Loc.GetString("plant-analyzer-harvest-autoharvest");

        seedChem += String.Join("\r\n   ", comp.Chemicals.Select(item => item.Key.ToString()));

        if (comp.ExudeGasses.Count > 0)
        {
            foreach (var (gas, amount) in comp.ExudeGasses)
            {
                exudeGases += "\r\n   " + gas;
            };
        }
        else
        {
            exudeGases = Loc.GetString("plant-analyzer-plant-gasses-No");
        }
        plantMutations = CheckAllMutation(comp, plantMutations);
        plantSpeciation += String.Join(", \r\n", comp.MutationPrototypes.Select(item => item.ToString()));

        //int count = comp.MutationPrototypes.Count();
        //plantMutations = count.ToString();
        var nutrientConsumption = comp.NutrientConsumption;
        var waterConsumption = comp.NutrientConsumption;
        var tolerances = "";
        tolerances = CheckAllTolerances(comp, tolerances);

        return new PlantAnalyzerScannedSeedPlantInformation(GetNetEntity(target), seedName, seedYield.ToString(), seedPotency.ToString(),
            seedChem, plantHarvestType, exudeGases, plantMutations, false, plantSpeciation, tolerances);
    }

    /// <summary>
    ///     Analysis of unique seed.
    /// </summary>
    public PlantAnalyzerScannedSeedPlantInformation ObtainingGeneDataSeed(SeedData comp, EntityUid target, bool trayChecker)
    {
        var seedName = Loc.GetString(comp.DisplayName);
        var seedYield = comp.Yield;
        var seedPotency = comp.Potency;
        var seedChem = "\r\n   ";
        var plantHarvestType = "";
        var plantMutations = "";
        var exudeGases = "";
        var isTray = trayChecker;

        var plantSpeciation = "";

        if (comp.HarvestRepeat == HarvestType.Repeat) plantHarvestType = Loc.GetString("plant-analyzer-harvest-repeat");
        if (comp.HarvestRepeat == HarvestType.NoRepeat) plantHarvestType = Loc.GetString("plant-analyzer-harvest-ephemeral");
        if (comp.HarvestRepeat == HarvestType.SelfHarvest) plantHarvestType = Loc.GetString("plant-analyzer-harvest-autoharvest");

        seedChem += String.Join("\r\n   ", comp.Chemicals.Select(item => item.Key.ToString()));

        if (comp.ExudeGasses.Count > 0)
        {
            foreach (var (gas, amount) in comp.ExudeGasses)
            {
                exudeGases += "\r\n   " + gas;
            };
        }
        else
        {
            exudeGases = Loc.GetString("plant-analyzer-plant-gasses-No");
        }
        plantMutations = CheckAllMutation(comp, plantMutations);
        plantSpeciation += String.Join(", \r\n", comp.MutationPrototypes.Select(item => item.ToString()));

        //int count = comp.MutationPrototypes.Count();
        //plantMutations = count.ToString();
        var nutrientConsumption = comp.NutrientConsumption;
        var waterConsumption = comp.NutrientConsumption;
        var tolerances = "";
        tolerances = CheckAllTolerances(comp, tolerances);

        return new PlantAnalyzerScannedSeedPlantInformation(GetNetEntity(target), seedName, seedYield.ToString(), seedPotency.ToString(),
            seedChem, plantHarvestType, exudeGases, plantMutations, trayChecker, plantSpeciation, tolerances.ToString());
    }

    /// <summary>
    ///     Returns mutations the seed has.
    /// </summary>
    public string CheckAllMutation(SeedData plant, string plantMutations)
    {
        if (plant.TurnIntoKudzu) plantMutations += "\r\n   " + $"{Loc.GetString("plant-analyzer-mutation-turnintokudzu")}";
        if (plant.Seedless) plantMutations += "\r\n   " + $"{Loc.GetString("plant-analyzer-mutation-seedless")}";
        if (plant.Slip) plantMutations += "\r\n   " + $"{Loc.GetString("plant-analyzer-mutation-slip")}";
        if (plant.Sentient) plantMutations += "\r\n   " + $"{Loc.GetString("plant-analyzer-mutation-sentient")}";
        if (plant.Ligneous) plantMutations += "\r\n   " + $"{Loc.GetString("plant-analyzer-mutation-ligneous")}";
        if (plant.Bioluminescent) plantMutations += "\r\n   " + $"{Loc.GetString("plant-analyzer-mutation-bioluminescent")}";
        if (plant.CanScream) plantMutations += "\r\n   " + $"{Loc.GetString("plant-analyzer-mutation-canscream")}";

        return plantMutations;
    }

    public string CheckAllTolerances(SeedData plant, string tolerances)
    {
        var nutrientConsumption = plant.NutrientConsumption;
        var waterConsumption = plant.NutrientConsumption;
        var idealHeat = plant.IdealHeat;
        var heatTolerance = plant.HeatTolerance;
        var idealLight = plant.IdealLight;
        var lightTolerance = plant.LightTolerance;
        var toxinsTolerance = plant.ToxinsTolerance;
        var lowPresssureTolerance = plant.LowPressureTolerance;
        var highPressureTolerance = plant.HighPressureTolerance;
        var pestTolerance = plant.PestTolerance;
        var weedTolerance = plant.WeedTolerance;

        var lifespan = plant.Lifespan;
        var maturation = plant.Maturation;
        var growthStages = plant.GrowthStages;

        tolerances = nutrientConsumption + ";" + waterConsumption + ";" + idealHeat + ";" + heatTolerance + ";" + idealLight + ";" + lightTolerance + ";"
                   + toxinsTolerance + ";" + lowPresssureTolerance + ";" + highPressureTolerance + ";" + pestTolerance + ";" + weedTolerance + ";"
                   + lifespan + ";" + maturation + ";" + growthStages;

        return tolerances;
    }

    public string CheckAllGeneralTraits(SeedData plant, string generalTraits)
    {

        var endurance = plant.Endurance;
        var yield = plant.Yield;
        var lifespan = plant.Lifespan;
        var maturation = plant.Maturation;
        var growthStages = plant.GrowthStages;
        var potency = plant.Potency;

        generalTraits = lifespan + ";\r\n" + maturation + ";\r\n" + growthStages;

        return generalTraits;
    }
}
