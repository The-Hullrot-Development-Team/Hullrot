using System.Linq;
using Content.Client.Popups;
using Content.Client.UserInterface.Controls;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.RCD;

[UsedImplicitly]
public sealed class RCDMenuBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;

    private SimpleRadialMenu? _menu;

    private static readonly Dictionary<string, (string Tooltip, SpriteSpecifier Sprite)> PrototypesGroupingInfo
        = new Dictionary<string, (string Tooltip, SpriteSpecifier Sprite)>
    {
        ["WallsAndFlooring"] = ("rcd-component-walls-and-flooring", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/walls_and_flooring.png"))),
        ["WindowsAndGrilles"] = ("rcd-component-windows-and-grilles", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/windows_and_grilles.png"))),
        ["Airlocks"] = ("rcd-component-airlocks", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/airlocks.png"))),
        ["Electrical"] = ("rcd-component-electrical", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/multicoil.png"))),
        ["Lighting"] = ("rcd-component-lighting", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/lighting.png"))),
    };

    public RCDMenuBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<RCDComponent>(Owner, out var rcd))
            return;

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        var models = ConvertToButtons(rcd.AvailablePrototypes);
        _menu.SetButtons(models);

        _menu.Open();
    }

    private IEnumerable<RadialMenuNestedLayerOption> ConvertToButtons(HashSet<ProtoId<RCDPrototype>> prototypes)
    {
        var models = prototypes.Select(x => _prototypeManager.Index(x))
                        .GroupBy(x => x.Category)
                        .Where(x => PrototypesGroupingInfo.ContainsKey(x.Key))
                        .Select(x =>
                        {
                            var nested = x.Select(proto => new RadialMenuActionOption<RCDPrototype>(HandleMenuOptionClick, proto)
                            {
                                Sprite = proto.Sprite,
                                ToolTip = GetTooltip(proto)
                            }).ToArray();

                            var tuple = PrototypesGroupingInfo[x.Key];
                            return new RadialMenuNestedLayerOption(nested)
                            {
                                Sprite = tuple.Sprite,
                                ToolTip = Loc.GetString(tuple.Tooltip)
                            };
                        });
        return models;
    }

    private void HandleMenuOptionClick(RCDPrototype proto)
    {
        // A predicted message cannot be used here as the RCD UI is closed immediately
        // after this message is sent, which will stop the server from receiving it
        SendMessage(new RCDSystemMessage(proto.ID));


        if (_playerManager.LocalSession?.AttachedEntity == null)
            return;

        var msg = Loc.GetString("rcd-component-change-mode", ("mode", Loc.GetString(proto.SetName)));

        if (proto.Mode is RcdMode.ConstructTile or RcdMode.ConstructObject)
        {
            var name = Loc.GetString(proto.SetName);

            if (proto.Prototype != null &&
                _prototypeManager.TryIndex(proto.Prototype, out var entProto, logError: false))
                name = entProto.Name;

            msg = Loc.GetString("rcd-component-change-build-mode", ("name", name));
        }

        // Popup message
        var popup = EntMan.System<PopupSystem>();
        popup.PopupClient(msg, Owner, _playerManager.LocalSession.AttachedEntity);
    }

    private string GetTooltip(RCDPrototype proto)
    {
        string tooltip;

        if (proto.Mode is RcdMode.ConstructTile or RcdMode.ConstructObject
            && proto.Prototype != null
            && _prototypeManager.TryIndex(proto.Prototype, out var entProto, logError: false))
        {
            tooltip = Loc.GetString(entProto.Name);
        }
        else
        {
            tooltip = Loc.GetString(proto.SetName);
        }

        tooltip = OopsConcat(char.ToUpper(tooltip[0]).ToString(), tooltip.Remove(0, 1));

        return tooltip;
    }

    private static string OopsConcat(string a, string b)
    {
        // This exists to prevent Roslyn being clever and compiling something that fails sandbox checks.
        return a + b;
    }
}
