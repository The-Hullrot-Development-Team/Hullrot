using Content.Client.UserInterface.Controls;
using Content.Shared.Mining;
using Content.Shared.Silicons.StationAi;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;
using System.Numerics;

namespace Content.Client.Silicons.StationAi;

[GenerateTypedNameReferences]
public sealed partial class StationAiCustomizationMenu : FancyWindow
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    private readonly SharedStationAiSystem _stationAiSystem = default!;

    private EntityUid? _owner = null;
    private Dictionary<ProtoId<StationAiCustomizationGroupPrototype>, StationAiCustomizationGroupContainer> _groupContainers = new();

    public event Action<ProtoId<StationAiCustomizationGroupPrototype>, ProtoId<StationAiCustomizationPrototype>>? SendStationAiCustomizationMessageAction;

    public StationAiCustomizationMenu()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        _stationAiSystem = _entManager.System<SharedStationAiSystem>();

        Title = Loc.GetString("station-ai-customization-menu");

        var groupPrototypes = _protoManager.EnumeratePrototypes<StationAiCustomizationGroupPrototype>();
        groupPrototypes = groupPrototypes.OrderBy(x => x.ID); // To ensure consistency in presention

        foreach (var groupPrototype in groupPrototypes)
        {
            var protoIds = groupPrototype.Prototypes.OrderBy(x => x.Id).ToList();

            _groupContainers[groupPrototype] = new StationAiCustomizationGroupContainer(protoIds, groupPrototype.Category, groupPrototype.PreviewKey, _protoManager);
            CustomizationGroupsContainer.AddTab(_groupContainers[groupPrototype], groupPrototype.Name);
        }
    }

    public void SetOwner(EntityUid owner)
    {
        _owner = owner;
        UpdateState();
    }

    public void UpdateState()
    {
        if (_owner == null || !_entManager.TryGetComponent<StationAiCoreComponent>(_owner, out var stationAiCore))
            return;

        if (!_stationAiSystem.TryGetInsertedAI((_owner.Value, stationAiCore), out var insertedAi))
            return;

        if (!_entManager.TryGetComponent<StationAiCustomizationComponent>(insertedAi, out var stationAiCustomization))
            return;

        foreach (var (groupProtoId, groupContainer) in _groupContainers)
        {
            if (stationAiCustomization.ProtoIds.TryGetValue(groupProtoId, out var protoId))
            {
                foreach (var child in groupContainer.Children)
                {
                    if (child is not StationAiCustomizationEntryContainer entry)
                        continue;

                    entry.SelectButton.Pressed = (entry.Prototype.ID == protoId);
                }
            }
        }
    }

    public void OnSendStationAiCustomizationMessage
        (ProtoId<StationAiCustomizationGroupPrototype> groupProtoId, ProtoId<StationAiCustomizationGroupPrototype> customizationProtoId)
    {
        SendStationAiCustomizationMessageAction?.Invoke(groupProtoId, customizationProtoId);
    }

    private sealed class StationAiCustomizationGroupContainer : BoxContainer
    {
        public StationAiCustomizationGroupContainer
            (List<ProtoId<StationAiCustomizationPrototype>> protoIds, StationAiCustomizationType category, string key, IPrototypeManager protoManager)
        {
            Orientation = LayoutOrientation.Vertical;
            HorizontalExpand = true;
            VerticalExpand = true;

            foreach (var protoId in protoIds)
            {
                if (!protoManager.TryIndex(protoId, out var prototype))
                    continue;

                var rsiPath = prototype.LayerData[key].RsiPath;
                var rsiState = prototype.LayerData[key].State;

                var entry = new StationAiCustomizationEntryContainer(prototype, category, rsiPath, rsiState);
                AddChild(entry);
            }
        }
    }

    private sealed class StationAiCustomizationEntryContainer : BoxContainer
    {
        public StationAiCustomizationPrototype Prototype;
        public Button SelectButton;

        public StationAiCustomizationEntryContainer(StationAiCustomizationPrototype prototype, StationAiCustomizationType category, string? rsiPath, string? rsiState)
        {
            Prototype = prototype;

            Orientation = LayoutOrientation.Horizontal;
            HorizontalExpand = true;

            SelectButton = new Button
            {
                Text = Loc.GetString(prototype.Name),
                HorizontalExpand = true,
                ToggleMode = true
            };

            SelectButton.OnPressed += args =>
            {
                SelectButton.Pressed = true;

                if (this.Parent == null)
                    return;

                foreach (var child in this.Parent.Children)
                {
                    if (child is not StationAiCustomizationEntryContainer entry)
                        continue;

                    if (entry == this)
                        continue;

                    entry.SelectButton.Pressed = false;
                }

                var parent = this.Parent;

                while (parent != null && parent is not StationAiCustomizationMenu)
                    parent = parent.Parent;

                (parent as StationAiCustomizationMenu)?.OnSendStationAiCustomizationMessage(prototype, category);
            };

            AddChild(SelectButton);

            var icon = new AnimatedTextureRect
            {
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                SetWidth = 56,
                SetHeight = 56,
                Margin = new Thickness(10f, 2f)
            };

            if (rsiPath != null && rsiState != null)
            {
                var specifier = new SpriteSpecifier.Rsi(new ResPath(rsiPath), rsiState);
                icon.SetFromSpriteSpecifier(specifier);
            }

            icon.DisplayRect.TextureScale = new Vector2(2f, 2f);

            AddChild(icon);
        }
    }
}


