﻿using Content.Client.ContextMenu.UI;
using Content.Client.Stylesheets;
using Content.Shared.Verbs;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Client.Utility;
using Robust.Shared.Utility;

namespace Content.Client.Examine;

/// <summary>
///     Buttons that show up in the examine tooltip to specify more detailed
///     ways to examine an item.
/// </summary>
public sealed class ExamineButton : ContainerButton
{
    public const string StyleClassExamineButton = "examine-button";

    public const int ElementHeight = 32;
    public const int ElementWidth = 32;

    private const int Thickness = 4;

    public TextureRect Icon;

    public ExamineVerb Verb;

    public ExamineButton(ExamineVerb verb)
    {
        Margin = new Thickness(Thickness, Thickness, Thickness, Thickness);

        SetOnlyStyleClass(StyleClassExamineButton);

        Verb = verb;

        if (verb.Disabled)
        {
            Disabled = true;
        }

        ToolTip = verb.Message ?? verb.Text;
        TooltipDelay = 0.2f; // if you're hovering over these icons, you probably want to know what they do.

        Icon = new TextureRect
        {
            SetWidth = ElementWidth,
            SetHeight = ElementHeight
        };

        if (verb.IconTexture != null)
        {
            var icon = new SpriteSpecifier.Texture(new ResourcePath(verb.IconTexture));

            Icon.Texture = icon.Frame0();
            Icon.Stretch = TextureRect.StretchMode.KeepAspectCentered;

            AddChild(Icon);
        }
    }
}
