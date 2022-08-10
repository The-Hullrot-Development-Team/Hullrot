using Content.Shared.CharacterAppearance;
using Content.Shared.Markings;
using Robust.Shared.Serialization;

namespace Content.Shared.Humanoid;

public enum HumanoidVisualizerKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class HumanoidVisualizerData
{
    public HumanoidVisualizerData(string species, Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> customBaseLayerInfo, Color skinColor, List<HumanoidVisualLayers> layerVisibility, List<Marking> markings)
    {
        Species = species;
        CustomBaseLayerInfo = customBaseLayerInfo;
        SkinColor = skinColor;
        LayerVisibility = layerVisibility;
        Markings = markings;
    }

    public string Species { get; }
    public Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> CustomBaseLayerInfo { get; }
    public Color SkinColor { get; }
    public List<HumanoidVisualLayers> LayerVisibility { get; }
    public List<Marking> Markings { get; }
}
