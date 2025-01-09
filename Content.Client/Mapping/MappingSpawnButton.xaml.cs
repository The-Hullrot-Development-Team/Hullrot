﻿using System.Numerics;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Mapping;

[GenerateTypedNameReferences]
public sealed partial class MappingSpawnButton : Control
{
    private MappingPrototype? _prototype;

    public MappingPrototype? Prototype
    {
        get => _prototype;
        set
        {
            _prototype = value;
            if (_prototype != null)
                ToggleFavorite(_prototype.Favorite);
        }
    }

    public MappingSpawnButton()
    {
        RobustXamlLoader.Load(this);

        CollapseTexture.TexturePath = "/Textures/Interface/VerbIcons/chevron-right-solid.svg.192dpi.png";
        FavoriteTexture.TexturePath = "/Textures/Interface/VerbIcons/star-regular.svg.192dpi.png";
        OnResized += OnResizedGallery;
        FavoriteButton.OnPressed += args => ToggleFavorite(args.Button.Pressed);
    }

    private void OnResizedGallery()
    {
        if (Parent != null)
            ChildrenPrototypesGallery.MaxGridWidth = Math.Max(1, Parent.Width - ChildrenPrototypesGallery.Margin.Left );
    }

    public void Gallery()
    {
        Button.ToolTip = Label.Text;
        Label.Visible = false;
        Button.AddStyleClass("ButtonSquare");
        SetWidth = 48;
        SetHeight = 48;
        FavoriteButton.Visible = false;
    }

    public void SetTextures(List<Texture> textures)
    {
        Button.RemoveStyleClass("OpenBoth");
        Button.AddStyleClass("OpenLeft");
        CollapseButton.RemoveStyleClass("OpenRight");
        CollapseButton.AddStyleClass("ButtonSquare");
        Texture.Visible = true;
        Texture.Textures.AddRange(textures);

        foreach (var texture in textures)
        {
            Texture.TextureScale = new Vector2(Texture.SetSize.X / texture.Height, Texture.SetSize.X / texture.Height);
        }

        Texture.InvalidateMeasure();
    }

    public void Collapse()
    {
        CollapseButton.Pressed = false;
        ChildrenPrototypes.DisposeAllChildren();
        ChildrenPrototypesGallery.DisposeAllChildren();
        CollapseTexture.TexturePath = "/Textures/Interface/VerbIcons/chevron-right-solid.svg.192dpi.png";
    }

    public void UnCollapse()
    {
        CollapseButton.Pressed = true;
        CollapseTexture.TexturePath = "/Textures/Interface/VerbIcons/chevron-down-solid.svg.192dpi.png";
    }

    public void ToggleFavorite(bool enabled)
    {
        FavoriteButton.Pressed = enabled;
        FavoriteTexture.TexturePath = FavoriteButton.Pressed
            ? "/Textures/Interface/VerbIcons/star-solid-yellow.svg.192dpi.png"
            : "/Textures/Interface/VerbIcons/star-regular.svg.192dpi.png";
    }
}
