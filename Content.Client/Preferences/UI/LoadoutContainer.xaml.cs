using Content.Shared.Clothing;
using Content.Shared.Preferences.Loadouts;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Preferences.UI;

[GenerateTypedNameReferences]
public sealed partial class LoadoutContainer : BoxContainer
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    public event Action<ProtoId<LoadoutPrototype>?>? OnLoadoutPressed;

    private EntityUid? _entity;

    public LoadoutContainer(ProtoId<LoadoutPrototype>? proto, ButtonGroup group, bool disabled, FormattedMessage? reason)
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        SelectButton.Disabled = disabled;
        SelectButton.Group = group;

        SelectButton.OnPressed += args =>
        {
            OnLoadoutPressed?.Invoke(proto);
        };

        if (disabled && reason != null)
        {
            var tooltip = new Tooltip();
            tooltip.SetMessage(reason);
            SelectButton.TooltipSupplier = _ => tooltip;
        }

        if (proto != null && _protoManager.TryIndex(proto, out var loadProto))
        {
            var ent = _entManager.System<LoadoutSystem>().GetFirstOrNull(loadProto);

            if (ent != null)
            {
                _entity = _entManager.SpawnEntity(ent, MapCoordinates.Nullspace);
                Sprite.SetEntity(_entity);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _entManager.DeleteEntity(_entity);
    }

    public bool Pressed
    {
        get => SelectButton.Pressed;
        set => SelectButton.Pressed = value;
    }

    public string? Text
    {
        get => SelectButton.Text;
        set => SelectButton.Text = value;
    }
}
