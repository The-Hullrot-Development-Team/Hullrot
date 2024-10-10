using System.Linq;
using Content.Shared.Ghost.Roles;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Ghost.Controls.Roles
{
    [GenerateTypedNameReferences]
    public sealed partial class GhostRolesWindow : DefaultWindow
    {
        public event Action<GhostRoleInfo>? OnRoleRequestButtonClicked;
        public event Action<GhostRoleInfo>? OnRoleFollow;

        public void ClearEntries()
        {
            NoRolesMessage.Visible = true;
            EntryContainer.DisposeAllChildren();
        }

        public IEnumerable<Collapsible> GetAllCollapsibleBoxes()
        {
            return EntryContainer.Children.OfType<Collapsible>();
        }

        public Collapsible? GetCollapsibleById(string id)
        {
            return EntryContainer.Children
                .OfType<Collapsible>()
                .FirstOrDefault(c => c.Name == id);
        }

        public Dictionary<int, bool> SaveCollapsibleBoxesStates()
        {
            var collapsibleStates = new Dictionary<int, bool>();
            foreach (var collapsible in GetAllCollapsibleBoxes())
            {
                if (int.TryParse(collapsible.Name, out var collapsibleId))
                {
                    collapsibleStates[collapsibleId] = collapsible.BodyVisible;
                }
            }
            return collapsibleStates;
        }

        public void RestoreCollapsibleBoxesStates(Dictionary<int, bool> collapsibleStates)
        {
            foreach (var collapsible in GetAllCollapsibleBoxes())
            {
                if (int.TryParse(collapsible.Name, out var collapsibleId) && collapsibleStates.TryGetValue(collapsibleId, out var isOpen))
                {
                    collapsible.BodyVisible = isOpen;
                }
            }
        }

        public void AddEntry(string name, string description, bool hasAccess, FormattedMessage? reason, IEnumerable<GhostRoleInfo> roles, SpriteSystem spriteSystem)
        {
            NoRolesMessage.Visible = false;

            var ghostRoleInfos = roles.ToList();
            var rolesCount = ghostRoleInfos.Count;

            var info = new GhostRoleInfoBox(name, description);
            var buttons = new GhostRoleButtonsBox(hasAccess, reason, ghostRoleInfos, spriteSystem);
            buttons.OnRoleSelected += OnRoleRequestButtonClicked;
            buttons.OnRoleFollow += OnRoleFollow;

            EntryContainer.AddChild(info);

            if (rolesCount > 1)
            {
                var buttonHeading = new CollapsibleHeading(Loc.GetString("ghost-roles-window-available-button", ("rolesCount", rolesCount)));

                buttonHeading.AddStyleClass(ContainerButton.StyleClassButton);
                buttonHeading.HorizontalAlignment = HAlignment.Stretch;
                buttonHeading.Label.HorizontalAlignment = HAlignment.Center;
                buttonHeading.Label.HorizontalExpand = true;
                buttonHeading.HorizontalExpand = true;

                var body = new CollapsibleBody
                {
                    Margin = new Thickness(0, 10, 0, 0),
                };

                var uniqueId = name.GetHashCode();

                var collapsible = new Collapsible(buttonHeading, body)
                {
                    Name = uniqueId.ToString(),
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    Margin = new Thickness(0, 0, 0, 8),
                };

                body.AddChild(buttons);

                EntryContainer.AddChild(collapsible);
            }
            else
            {
                EntryContainer.AddChild(buttons);
            }
        }
    }
}
