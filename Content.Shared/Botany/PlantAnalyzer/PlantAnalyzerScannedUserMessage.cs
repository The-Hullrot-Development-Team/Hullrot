using Content.Shared.Atmos;
using Robust.Shared.Serialization;

namespace Content.Shared.Botany.PlantAnalyzer;

[Serializable, NetSerializable]
public sealed class PlantAnalyzerScannedUserMessage : BoundUserInterfaceMessage
{
    // TODO: PA
    public readonly NetEntity? TargetEntity;
    public bool? ScanMode;
    public string? SeedDisplayName;
    public PlantAnalyzerTrayData? TrayData;
    public PlantAnalyzerTolerancesData? TolerancesData;


    public PlantAnalyzerScannedUserMessage(NetEntity? targetEntity, bool? scanMode, string? seedDisplayName, PlantAnalyzerTrayData? trayData, PlantAnalyzerTolerancesData? tolerancesData)
    {
        TargetEntity = targetEntity;
        ScanMode = scanMode;
        SeedDisplayName = seedDisplayName;
        TrayData = trayData;
        TolerancesData = tolerancesData;
    }
}

/// <summary>
/// Everything that is kept independed of a given plant/seed.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlantAnalyzerTrayData(float waterLevel, float nutritionLevel, float toxins, float pestLevel, float weedLevel)
{
    public float WaterLevel = waterLevel;
    public float NutritionLevel = nutritionLevel;
    public float Toxins = toxins;
    public float PestLevel = pestLevel;
    public float WeedLevel = weedLevel;
}


/// <summary>
/// All the information to keep the plant alive.
/// Which is most of the "Tolerances" reagion plus the gases it may need.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlantAnalyzerTolerancesData(float nutrientConsumption, float waterConsumption, float idealHeat, float heatTolerance, float idealLight, float lightTolerance, float toxinsTolerance, float lowPressureTolerance, float highPressureTolerance, float pestTolerance, float weedTolerance, List<Gas> consumeGasses)
{
    public float WaterConsumption = waterConsumption;
    public float NutrientConsumption = nutrientConsumption;
    public float ToxinsTolerance = toxinsTolerance;
    public float PestTolerance = pestTolerance;
    public float WeedTolerance = weedTolerance;
    public float LowPressureTolerance = lowPressureTolerance;
    public float HighPressureTolerance = highPressureTolerance;
    public float IdealHeat = idealHeat;
    public float HeatTolerance = heatTolerance;
    public float IdealLight = idealLight;
    public float LightTolerance = lightTolerance;
    public List<Gas> ConsumeGasses = consumeGasses;
}
