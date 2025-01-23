using Robust.Client.UserInterface;
using System.Numerics;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Shared.Utility;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.UserInterface.Controls;

[GenerateTypedNameReferences]
public partial class SimpleRadialMenu : RadialMenu
{
    private readonly EntityUid? _attachMenuToEntity;

    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;

    /// <summary>
    /// c-tor for codegen to work properly, is not used in runtime and should not be called in code.
    /// </summary>
    public SimpleRadialMenu()
    {
        // no-op
    }

    public SimpleRadialMenu(IEnumerable<RadialMenuOption> models, EntityUid? attachMenuToEntity = null, int initialContainerRadius = 100)
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);

        _attachMenuToEntity = attachMenuToEntity;
        var sprites = _entManager.System<SpriteSystem>();

        Fill(models, sprites, Children, initialContainerRadius);
    }

    private void Fill(
        IEnumerable<RadialMenuOption> models,
        SpriteSystem sprites,
        ICollection<Control> rootControlChildren,
        int initialContainerRadius
    )
    {
        var rootContainer = new RadialContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            InitialRadius = initialContainerRadius,
            ReserveSpaceForHiddenChildren = false,
            Visible = true
        };
        rootControlChildren.Add(rootContainer);

        foreach (var model in models)
        {
            if (model is RadialMenuNestedLayerOption nestedMenuModel)
            {
                var linkButton = RecursiveContainerExtraction(sprites, rootControlChildren, nestedMenuModel);
                linkButton.Visible = true;
                rootContainer.AddChild(linkButton);
            }
            else
            {
                var rootButtons = ConvertToButton(model, sprites, false);
                rootContainer.AddChild(rootButtons);
            }
        }
    }

    private RadialMenuTextureButtonWithSector RecursiveContainerExtraction(
        SpriteSystem sprites,
        ICollection<Control> rootControlChildren,
        RadialMenuNestedLayerOption model
    )
    {
        var container = new RadialContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            InitialRadius = model.ContainerRadius!.Value,
            ReserveSpaceForHiddenChildren = false,
            Visible = false
        };
        foreach (var nested in model.Nested)
        {
            if (nested is RadialMenuNestedLayerOption nestedMenuModel)
            {
                var linkButton = RecursiveContainerExtraction(sprites, rootControlChildren, nestedMenuModel);
                container.AddChild(linkButton);
            }
            else
            {
                var button = ConvertToButton(nested, sprites, false);
                container.AddChild(button);
            }
        }
        rootControlChildren.Add(container);

        var thisLayerLinkButton = ConvertToButton(model, sprites, true);
        thisLayerLinkButton.TargetLayer = container;
        return thisLayerLinkButton;
    }

    private RadialMenuTextureButtonWithSector ConvertToButton(
        RadialMenuOption model,
        SpriteSystem sprites,
        bool haveNested
    )
    {
        var button = new RadialMenuTextureButtonWithSector
        {
            SetSize = new Vector2(64f, 64f),
            ToolTip = model.ToolTip,
        };
        if (model.Sprite != null)
        {
            button.TextureNormal = sprites.Frame0(model.Sprite);
        }
        button.OnPressed += _ =>
        {
            model.OnPressed?.Invoke();
            if(!haveNested)
                Close();
        };
        return button;
    }

    #region target entity tracking

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_attachMenuToEntity != null)
        {
            UpdatePosition();
        }
    }

    private void UpdatePosition()
    {
        if (!_entManager.TryGetComponent(_attachMenuToEntity, out TransformComponent? xform))
        {
            Close();
            return;
        }

        if (!xform.Coordinates.IsValid(_entManager))
        {
            Close();
            return;
        }

        var coords = _entManager.System<SpriteSystem>().GetSpriteScreenCoordinates((_attachMenuToEntity.Value, null, xform));

        if (!coords.IsValid)
        {
            Close();
            return;
        }

        OpenScreenAt(coords.Position, _clyde);
    }

    #endregion
}


public abstract class RadialMenuOption
{
    public string? ToolTip;
    
    public SpriteSpecifier? Sprite { get; init; }

    public Action? OnPressed { get; protected set; }
}

public class RadialMenuActionOption : RadialMenuOption
{
    public RadialMenuActionOption(Action onPressed)
    {
        OnPressed = onPressed;
    }
}

public class RadialMenuNestedLayerOption : RadialMenuOption
{
    public RadialMenuNestedLayerOption(IReadOnlyCollection<RadialMenuOption> nested, float containerRadius = 100)
    {
        Nested = nested;
        ContainerRadius = 100;
    }

    public float? ContainerRadius { get; }

    public IReadOnlyCollection<RadialMenuOption> Nested { get; }

}
