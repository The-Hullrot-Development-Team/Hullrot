﻿#nullable enable
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.GameObjects;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.Ghost.Roles.UI
{
    [GenerateTypedNameReferences]
    public partial class MakeGhostRoleWindow : SS14Window
    {
        public delegate void MakeRole(EntityUid uid, string name, string description, bool makeSentient);

        public MakeGhostRoleWindow()
        {
            RobustXamlLoader.Load(this);

            MakeSentientLabel.MinSize = (150, 0);
            RoleEntityLabel.MinSize = (150, 0);
            RoleNameLabel.MinSize = (150, 0);
            RoleName.MinSize = (300, 0);
            RoleDescriptionLabel.MinSize = (150, 0);
            RoleDescription.MinSize = (300, 0);

            MakeButton.OnPressed += OnPressed;
        }

        private EntityUid? EntityUid { get; set; }

        public event MakeRole? OnMake;

        public void SetEntity(EntityUid uid)
        {
            EntityUid = uid;
            RoleEntity.Text = $"{uid}";
        }

        private void OnPressed(ButtonEventArgs args)
        {
            if (EntityUid == null)
            {
                return;
            }

            OnMake?.Invoke(EntityUid.Value, RoleName.Text, RoleDescription.Text, MakeSentientCheckbox.Pressed);
        }
    }
}
