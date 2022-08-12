using Content.Client.Administration.Managers;
using Content.Client.Stylesheets;
using Content.Shared.Ghost.Roles;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Timing;

namespace Content.Client.Ghost.Roles.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class GhostRolesWindow : DefaultWindow
    {
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly IClientAdminManager _adminManager = default!;

        public event Action<GhostRoleInfo>? OnRoleTake;
        public event Action<GhostRoleInfo>? OnRoleRequested;
        public event Action<GhostRoleInfo>? OnRoleCancelled;
        public event Action<GhostRoleInfo>? OnRoleFollowed;

        public event Action<GhostRoleGroupInfo>? OnGroupRequested;
        public event Action<GhostRoleGroupInfo>? OnGroupCancelled;
        public event Action? OnRoleGroupsOpened;

        public GhostRolesWindow()
        {
            RobustXamlLoader.Load(this);
            IoCManager.InjectDependencies(this);

            AdminControls.Visible = _adminManager.IsActive();
            TimeRemainingProgress.ForegroundStyleBoxOverride = new StyleBoxFlat(StyleNano.NanoGold);

            OpenGhostRoleGroupsButton.OnPressed += _ => OnRoleGroupsOpened?.Invoke();
        }

        public void SetLotteryTime(TimeSpan lotteryStart, TimeSpan lotteryEnd)
        {
            // Negate the values to remove the need to do calculations.
            TimeRemainingProgress.MinValue = (float) -lotteryEnd.TotalSeconds;
            TimeRemainingProgress.MaxValue = (float) -lotteryStart.TotalSeconds;
            TimeRemainingProgress.Value = (float) -lotteryStart.TotalSeconds;
        }

        public void ClearEntries()
        {
            NoRolesMessage.Visible = true;
            EntryContainer.DisposeAllChildren();
        }

        public void AddEntry(GhostRoleInfo role, bool isRequested)
        {
            NoRolesMessage.Visible = false;

            var entry = new GhostRoleEntry(role, isRequested);
            entry.OnRoleTake += OnRoleTake;
            entry.OnRoleSelected += OnRoleRequested;
            entry.OnRoleCancelled += OnRoleCancelled;
            entry.OnRoleFollowed += OnRoleFollowed;
            EntryContainer.AddChild(entry);
        }

        public void AddGroupEntry(GhostRoleGroupInfo group, bool adminControls, bool isRequested)
        {
            NoRolesMessage.Visible = false;

            var entry = new GhostRoleGroupEntry(group, adminControls, isRequested);
            entry.OnGroupSelected +=  OnGroupRequested;
            entry.OnGroupCancelled += OnGroupCancelled;

            EntryContainer.AddChild(entry);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            TimeRemainingProgress.Value = (float) -_gameTiming.CurTime.TotalSeconds;
        }
    }
}
