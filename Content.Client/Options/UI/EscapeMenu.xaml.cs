using Content.Client.Stylesheets;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Options.UI;

[GenerateTypedNameReferences]
public sealed partial class EscapeMenu : DefaultFullscreen
{
    public EscapeMenu()
    {
        RobustXamlLoader.Load(this);
    }
}
