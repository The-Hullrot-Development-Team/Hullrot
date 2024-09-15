using Content.Shared.Administration.BanList;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.Administration.UI.BanList.RoleBans;

[GenerateTypedNameReferences]
public sealed partial class RoleBanListLine : BoxContainer, IBanListLine<SharedServerRoleBan>
{
    public SharedServerRoleBan Ban { get; }

    public event Action<RoleBanListLine>? IdsClicked;

    public RoleBanListLine(SharedServerRoleBan ban)
    {
        RobustXamlLoader.Load(this);

        Ban = ban;
        IdsHidden.OnPressed += IdsPressed;

        BanListEui.SetData(this, ban);
        Role.Text = ban.Role;
    }

    private void IdsPressed(ButtonEventArgs buttonEventArgs)
    {
        IdsClicked?.Invoke(this);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        IdsHidden.OnPressed -= IdsPressed;
        IdsClicked = null;
    }
}

