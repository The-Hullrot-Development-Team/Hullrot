using System.Linq;
using Content.Client.Eui;
using Content.Shared.Eui;
using Content.Shared.Ghost.Roles;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Client.Ghost.Roles.UI
{
    [UsedImplicitly]
    public sealed class GhostRolesEui : BaseEui
    {
        private readonly IGameTiming _timing;

        private readonly GhostRolesWindow _window;
        private GhostRoleRulesWindow? _windowRules = null;
        private uint _windowRulesId = 0;

        public GhostRolesEui()
        {
            _timing = IoCManager.Resolve<IGameTiming>();
            _window = new GhostRolesWindow();

            _window.OnRoleRequested += info =>
            {
                if (_windowRules != null)
                    _windowRules.Close();
                _windowRules = new GhostRoleRulesWindow(info.Rules, _ =>
                {
                    SendMessage(new GhostRoleTakeoverRequestMessage(info.Identifier));
                });
                _windowRulesId = info.Identifier;
                _windowRules.OnClose += () =>
                {
                    _windowRules = null;
                };
                _windowRules.OpenCentered();
            };

            _window.OnRoleCancelled += info =>
            {
               SendMessage(new GhostRoleCancelTakeoverRequestMessage(info.Identifier));
            };

            _window.OnRoleFollow += info =>
            {
                SendMessage(new GhostRoleFollowRequestMessage(info.Identifier));
            };

            _window.OnClose += () =>
            {
                SendMessage(new GhostRoleWindowCloseMessage());
            };
        }

        public override void Opened()
        {
            base.Opened();
            _window.OpenCentered();
        }

        public override void Closed()
        {
            base.Closed();
            _window.Close();
            _windowRules?.Close();
        }

        public override void HandleState(EuiStateBase state)
        {
            base.HandleState(state);

            if (state is not GhostRolesEuiState ghostState)
                return;

            _window.ClearEntries();

            var groupedRoles = ghostState.GhostRoles.GroupBy(
                role => (role.Name, role.Description));
            foreach (var group in groupedRoles)
            {
                var name = group.Key.Name;
                var description = group.Key.Description;

                _window.AddEntry(name, description, group, _timing);
            }

            var closeRulesWindow = ghostState.GhostRoles.All(role => role.Identifier != _windowRulesId);
            if (closeRulesWindow)
            {
                _windowRules?.Close();
            }
        }
    }
}
