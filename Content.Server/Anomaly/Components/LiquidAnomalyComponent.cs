using Content.Server.Anomaly.Effects;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Server.Anomaly.Components;

[RegisterComponent, Access(typeof(LiquidAnomalySystem))]
public sealed partial class LiquidAnomalyComponent : Component
{

    /// <summary>
    /// the total amount of reagent generated by the anomaly per pulse
    /// scales with Severity
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxSolutionGeneration = 100;
    /// <summary>
    /// the total amount of reagent generated by the anomaly in the supercritical phase
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SuperCriticalSolutionGeneration = 1000;


    /// <summary>
    /// the maximum amount of injection of a substance into an entity per pulsation
    /// scales with Severity
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxSolutionInjection = 15;
    /// <summary>
    /// the maximum amount of injection of a substance into an entity in the supercritical phase
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SuperCriticalSolutionInjection = 50;


    /// <summary>
    /// The maximum radius in which the anomaly injects reagents into the surrounding containers.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float InjectRadius = 3;
    /// <summary>
    /// The maximum radius in which the anomaly injects reagents into the surrounding containers.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SuperCriticalInjectRadius = 15;


    //The idea is to divide substances into several categories.
    //The anomaly will choose one of the categories with a given chance based on severity.
    //Then a random substance will be selected from the selected category.
    //There are the following categories:

    //Dangerous:
    //selected most often. A list of substances that are extremely unpleasant for injection.

    //Fun:
    //Funny things have an increased chance of appearing in an anomaly.

    //Useful:
    //Those reagents that the players are hunting for. Very low percentage of loss.

    //Other:
    //All reagents that exist in the game, with the exception of those prescribed in other lists and the blacklist.
    //They have low chances of appearing due to the fact that most things are boring and do not bring a
    //significant effect on the game.

    /// <summary>
    /// The spread of the random weight of the choice of this category, depending on the severity.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Vector2 WeightSpreadDangerous = new(5.0f, 9.0f);
    /// <summary>
    /// The spread of the random weight of the choice of this category, depending on the severity.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Vector2 WeightSpreadFun = new(3.0f, 0.0f);
    /// <summary>
    /// The spread of the random weight of the choice of this category, depending on the severity.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Vector2 WeightSpreadUseful = new(1.0f, 1.0f);
    /// <summary>
    /// The spread of the random weight of the choice of this category, depending on the severity.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Vector2 WeightSpreadOther = new(1.0f, 0.0f);


    /// <summary>
    /// Blocked reagents for the anomaly.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<ProtoId<ReagentPrototype>> BlacklistChemicals = new();
    /// <summary>
    /// Category of dangerous reagents for injection. Various toxins and poisons
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<ProtoId<ReagentPrototype>> DangerousChemicals = new();
    /// <summary>
    /// Category of useful reagents for injection. Medicine and other things that players WANT to get
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<ProtoId<ReagentPrototype>> UsefulChemicals = new();
    /// <summary>
    /// Category of fun reagents for injection. Glue, drugs, beer. Something that will bring fun.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<ProtoId<ReagentPrototype>> FunChemicals = new();


    /// <summary>
    /// Increasing severity by the specified amount will cause a change of reagent.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float ReagentChangeStep = 0.1f;

    /// <summary>
    /// If true, the sprite of the object will be colored in the color of the current reagent.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool NeedRecolorEntity = true;
    /// <summary>
    /// If true and if the entity has bloodstream, replaces the blood type with the anomaly reagent.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool NeedBloodstreamChange = true;

    /// <summary>
    /// Noise made when anomaly pulse.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier ChangeSound = new SoundPathSpecifier("/Audio/Effects/waterswirl.ogg");

    /// <summary>
    /// The name of the reagent that the anomaly produces.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string VisualEffectPrototype = "PuddleSparkle";

    /// <summary>
    /// The name of the reagent that the anomaly produces.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<ReagentPrototype> Reagent = "Water";
    /// <summary>
    /// The next threshold beyond which the anomaly will change its reagent.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float NextChangeThreshold = 0.1f;
}
