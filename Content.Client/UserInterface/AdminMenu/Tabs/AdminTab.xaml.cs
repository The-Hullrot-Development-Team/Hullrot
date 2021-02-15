﻿using System.Collections.Generic;
using System.Linq;
using Content.Client.UserInterface.AdminMenu.CustomControls;
using Robust.Client.AutoGenerated;
using Robust.Client.Console;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.IoC;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.AdminMenu.Tabs
{
    [GenerateTypedNameReferences]
    public partial class AdminTab : MarginContainer
    {
        private readonly List<AdminMenuControls.CommandButton> _adminButtons = new()
        {
            new KickCommandButton(),
            new BanCommandButton(),
            new AdminMenuControls.DirectCommandButton("Admin Ghost", "aghost"),
            new TeleportCommandButton(),
            new AdminMenuControls.DirectCommandButton("Permissions Panel", "permissions"),
        };

        public AdminTab()
        {
            IoCManager.InjectDependencies(this);
            RobustXamlLoader.Load(this);
            ButtonGridControl.AddCommandButtons(_adminButtons);
        }

        #region Command Buttons

        private class KickCommandButton : AdminMenuControls.UICommandButton
        {
            public override string Name => "Kick";
            public override string RequiredCommand => "kick";

            private readonly AdminMenuControls.CommandUIDropDown _playerDropDown = new()
            {
                Name = "Player",
                GetData = () => IoCManager.Resolve<IPlayerManager>().Sessions.ToList<object>(),
                GetDisplayName = (obj) =>
                    $"{((IPlayerSession) obj).Name} ({((IPlayerSession) obj).AttachedEntity?.Name})",
                GetValueFromData = (obj) => ((IPlayerSession) obj).Name,
            };

            private readonly AdminMenuControls.CommandUILineEdit _reason = new()
            {
                Name = "Reason"
            };

            public override List<AdminMenuControls.CommandUIControl> UI => new()
            {
                _playerDropDown,
                _reason
            };

            public override void Submit()
            {
                IoCManager.Resolve<IClientConsoleHost>().ExecuteCommand(
                    $"kick \"{_playerDropDown.GetValue()}\" \"{CommandParsing.Escape(_reason.GetValue())}\"");
            }
        }

        private class BanCommandButton : AdminMenuControls.UICommandButton
        {
            public override string Name => "Ban";
            public override string RequiredCommand => "ban";

            private readonly AdminMenuControls.CommandUIDropDown _playerDropDown = new()
            {
                Name = "Player",
                GetData = () => IoCManager.Resolve<IPlayerManager>().Sessions.ToList<object>(),
                GetDisplayName = (obj) =>
                    $"{((IPlayerSession) obj).Name} ({((IPlayerSession) obj).AttachedEntity?.Name})",
                GetValueFromData = (obj) => ((IPlayerSession) obj).Name,
            };

            private readonly AdminMenuControls.CommandUILineEdit _reason = new()
            {
                Name = "Reason"
            };

            private readonly AdminMenuControls.CommandUILineEdit _minutes = new()
            {
                Name = "Minutes"
            };

            public override List<AdminMenuControls.CommandUIControl> UI => new()
            {
                _playerDropDown,
                _reason,
                _minutes
            };

            public override void Submit()
            {
                IoCManager.Resolve<IClientConsoleHost>().ExecuteCommand(
                    $"ban \"{_playerDropDown.GetValue()}\" \"{CommandParsing.Escape(_reason.GetValue())}\" \"{_minutes.GetValue()}");
            }
        }

        private class TeleportCommandButton : AdminMenuControls.UICommandButton
        {
            public override string Name => "Teleport";
            public override string RequiredCommand => "tpto";

            private readonly AdminMenuControls.CommandUIDropDown _playerDropDown = new()
            {
                Name = "Player",
                GetData = () => IoCManager.Resolve<IPlayerManager>().Sessions.ToList<object>(),
                GetDisplayName = (obj) =>
                    $"{((IPlayerSession) obj).Name} ({((IPlayerSession) obj).AttachedEntity?.Name})",
                GetValueFromData = (obj) => ((IPlayerSession) obj).Name,
            };

            public override List<AdminMenuControls.CommandUIControl> UI => new()
            {
                _playerDropDown
            };

            public override void Submit()
            {
                IoCManager.Resolve<IClientConsoleHost>().ExecuteCommand($"tpto \"{_playerDropDown.GetValue()}\"");
            }
        }

        #endregion
    }
}
