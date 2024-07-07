﻿using Content.Client.Stylesheets.Redux.Fonts;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.Redux.StylesheetHelpers;

namespace Content.Client.Stylesheets.Redux.Sheetlets.Hud;

[CommonSheetlet]
public sealed class ItemStatusSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        return
        [
            E()
                .Class(StyleClass.StyleClassItemStatus)
                .Prop("font", sheet.BaseFont.GetFont(10)),

            E()
                .Class(StyleClass.StyleClassItemStatusNotHeld)
                .Prop("font", sheet.BaseFont.GetFont(10, FontStack.FontKind.Italic))
                .Prop("font-color", Color.Gray),

            E<RichTextLabel>()
                .Class(StyleClass.StyleClassItemStatus)
                .Prop(nameof(RichTextLabel.LineHeightScale), 0.7f)
                .Prop(nameof(Control.Margin), new Thickness(0, 0, 0, -6)),
        ];
    }
}
