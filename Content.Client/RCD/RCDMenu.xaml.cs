using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.Administration.Notes;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.RCD.Components;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Configuration;

namespace Content.Client.RCD;

[GenerateTypedNameReferences]
public sealed partial class RCDMenu : RadialMenu
{
    public RCDMenu(EntityUid owner)
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);

        this.VerticalExpand = true;
        this.HorizontalExpand = true;

        OnChildAdded += AddRCDMenuButtonOnClickActions;
    }

    private void AddRCDMenuButtonOnClickActions(Control control)
    {
        var radialContainer = control as RadialContainer;

        if (radialContainer == null)
            return;

        foreach (var child in radialContainer.Children)
        {
            var castChild = child as RadialMenuButton;

            if (castChild == null)
                continue;

            castChild.OnButtonUp += _ =>
            {
                //SendMessage();
            };
        }
    }
}

public sealed class RCDMenuButton : RadialMenuTextureButton
{
    public RcdMode RcdMode { get; set; }
    public string? ConstructionPrototype { get; set; }

    public RCDMenuButton()
    {

    }
}
