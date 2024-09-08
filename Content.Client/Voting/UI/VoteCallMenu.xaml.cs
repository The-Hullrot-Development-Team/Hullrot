using System.Linq;
using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Ghost;
using Content.Shared.Voting;
using JetBrains.Annotations;
using Robust.Client.AutoGenerated;
using Robust.Client.Console;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client.Voting.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class VoteCallMenu : BaseWindow
    {
        [Dependency] private readonly IClientConsoleHost _consoleHost = default!;
        [Dependency] private readonly IVoteManager _voteManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly IClientNetManager _netManager = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IEntityNetworkManager _entNetManager = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;

        private VotingSystem _votingSystem;

        public StandardVoteType Type;

        public Dictionary<StandardVoteType, CreateVoteOption> AvailableVoteOptions = new Dictionary<StandardVoteType, CreateVoteOption>()
        {
            { StandardVoteType.Restart, new CreateVoteOption("ui-vote-type-restart", new(), false, null) },
            { StandardVoteType.Preset, new CreateVoteOption("ui-vote-type-gamemode", new(), false, null) },
            { StandardVoteType.Map, new CreateVoteOption("ui-vote-type-map", new(), false, null) },
            { StandardVoteType.Votekick, new CreateVoteOption("ui-vote-type-votekick", new(), true, 0) }
        };

        public Dictionary<string, string> VotekickReasons = new Dictionary<string, string>()
        {
            { VotekickReasonType.Raiding.ToString(), Loc.GetString("ui-vote-votekick-type-raiding") },
            { VotekickReasonType.Cheating.ToString(), Loc.GetString("ui-vote-votekick-type-cheating") },
            { VotekickReasonType.Spam.ToString(), Loc.GetString("ui-vote-votekick-type-spamming") }
        };

        public Dictionary<NetUserId, (NetEntity, string)> PlayerList = new();

        public OptionButton? _followDropdown = null;

        public bool IsAllowedVotekick = false;

        public VoteCallMenu()
        {
            IoCManager.InjectDependencies(this);
            RobustXamlLoader.Load(this);
            _votingSystem = _entityManager.System<VotingSystem>();

            Stylesheet = IoCManager.Resolve<IStylesheetManager>().SheetSpace;
            CloseButton.OnPressed += _ => Close();
            VoteNotTrustedLabel.Text = Loc.GetString("ui-vote-trusted-users-notice", ("timeReq", _cfg.GetCVar(CCVars.VotekickEligibleVoterDeathtime) / 60));

            foreach (StandardVoteType voteType in Enum.GetValues<StandardVoteType>())
            {
                var option = AvailableVoteOptions[voteType];
                VoteTypeButton.AddItem(Loc.GetString(option.Name), (int)voteType);
            }

            VoteTypeButton.OnItemSelected += VoteTypeSelected;
            CreateButton.OnPressed += CreatePressed;
            FollowButton.OnPressed += FollowSelected;
        }

        protected override void Opened()
        {
            base.Opened();

            _netManager.ClientSendMessage(new MsgVoteMenu());

            _voteManager.CanCallVoteChanged += CanCallVoteChanged;
            _votingSystem.VotePlayerListResponse += UpdateVotePlayerList;
            _votingSystem.RequestVotePlayerList();
        }

        public override void Close()
        {
            base.Close();

            _voteManager.CanCallVoteChanged -= CanCallVoteChanged;
            _votingSystem.VotePlayerListResponse -= UpdateVotePlayerList;
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            UpdateVoteTimeout();
        }

        private void CanCallVoteChanged(bool obj)
        {
            if (!obj)
                Close();
        }

        private void UpdateVotePlayerList(VotePlayerListResponseEvent msg)
        {
            Dictionary<string, string> optionsList = new();
            Dictionary<NetUserId, (NetEntity, string)> playerList = new();
            foreach ((NetUserId, NetEntity, string) player in msg.Players)
            {
                optionsList.Add(player.Item1.ToString(), player.Item3);
                playerList.Add(player.Item1, (player.Item2, player.Item3));
            }
            if (optionsList.Count == 0)
                optionsList.Add(" ", " ");

            PlayerList = playerList;

            IsAllowedVotekick = !msg.Denied;

            var updatedDropdownOption = AvailableVoteOptions[StandardVoteType.Votekick];
            updatedDropdownOption.Dropdowns = new List<Dictionary<string, string>>() { optionsList, VotekickReasons };
            AvailableVoteOptions[StandardVoteType.Votekick] = updatedDropdownOption;
        }

        private void CreatePressed(BaseButton.ButtonEventArgs obj)
        {
            var typeId = VoteTypeButton.SelectedId;
            var voteType = AvailableVoteOptions[(StandardVoteType)typeId];

            var commandArgs = "";

            if (voteType.Dropdowns == null || voteType.Dropdowns.Count == 0)
            {
                _consoleHost.LocalShell.RemoteExecuteCommand($"createvote {((StandardVoteType)typeId).ToString()}");
            }
            else
            {
                int i = 0;
                foreach(var dropdowns in VoteOptionsButtonContainer.Children)
                {
                    if (dropdowns is OptionButton optionButton && AvailableVoteOptions[(StandardVoteType)typeId].Dropdowns != null)
                    {
                        commandArgs += AvailableVoteOptions[(StandardVoteType)typeId].Dropdowns[i].ElementAt(optionButton.SelectedId).Key + " ";
                        i++;
                    }
                }
                _consoleHost.LocalShell.RemoteExecuteCommand($"createvote {((StandardVoteType)typeId).ToString()} {commandArgs}");
            }

            Close();
        }

        private void UpdateVoteTimeout()
        {
            var typeKey = (StandardVoteType)VoteTypeButton.SelectedId;
            var isAvailable = _voteManager.CanCallStandardVote(typeKey, out var timeout);
            if (typeKey == StandardVoteType.Votekick && !IsAllowedVotekick)
            {
                CreateButton.Disabled = true;
            }
            else
            {
                CreateButton.Disabled = !isAvailable;
            }
            VoteTypeTimeoutLabel.Visible = !isAvailable;

            if (!isAvailable)
            {
                if (timeout == TimeSpan.Zero)
                {
                    VoteTypeTimeoutLabel.Text = Loc.GetString("ui-vote-type-not-available");
                }
                else
                {
                    var remaining = timeout - _gameTiming.RealTime;
                    VoteTypeTimeoutLabel.Text = Loc.GetString("ui-vote-type-timeout", ("remaining", remaining.ToString("mm\\:ss")));
                }
            }
        }

        private static void ButtonSelected(OptionButton.ItemSelectedEventArgs obj)
        {
            obj.Button.SelectId(obj.Id);
        }

        private void FollowSelected(Button.ButtonEventArgs obj)
        {
            if (_followDropdown == null)
                return;

            if (_followDropdown.SelectedId >= PlayerList.Count)
                return;

            var netEntity = PlayerList.ElementAt(_followDropdown.SelectedId).Value.Item1;

            var msg = new GhostWarpToTargetRequestEvent(netEntity);
            _entNetManager.SendSystemNetworkMessage(msg);
        }

        private void VoteTypeSelected(OptionButton.ItemSelectedEventArgs obj)
        {
            VoteTypeButton.SelectId(obj.Id);

            VoteNotTrustedLabel.Visible = false;
            if ((StandardVoteType)obj.Id == StandardVoteType.Votekick)
            {
                if (!IsAllowedVotekick)
                {
                    VoteNotTrustedLabel.Visible = true;
                    var updatedDropdownOption = AvailableVoteOptions[StandardVoteType.Votekick];
                    updatedDropdownOption.Dropdowns = new List<Dictionary<string, string>>();
                    AvailableVoteOptions[StandardVoteType.Votekick] = updatedDropdownOption;
                }
                else
                {
                    _votingSystem.RequestVotePlayerList();
                    VoteWarningLabel.Visible = AvailableVoteOptions[(StandardVoteType)obj.Id].EnableVoteWarning;
                }
            }


            FollowButton.Visible = false;

            var voteList = AvailableVoteOptions[(StandardVoteType)obj.Id].Dropdowns;

            VoteOptionsButtonContainer.RemoveAllChildren();
            if (voteList != null)
            {
                int i = 0;
                foreach (var voteDropdown in voteList)
                {
                    var optionButton = new OptionButton();
                    int j = 0;
                    foreach (var (key, value) in voteDropdown)
                    {
                        optionButton.AddItem(Loc.GetString(value), j);
                        j++;
                    }
                    VoteOptionsButtonContainer.AddChild(optionButton);
                    optionButton.Visible = true;
                    optionButton.OnItemSelected += ButtonSelected;
                    if (AvailableVoteOptions[(StandardVoteType)obj.Id].FollowDropdownId != null && AvailableVoteOptions[(StandardVoteType)obj.Id].FollowDropdownId == i)
                    {
                        _followDropdown = optionButton;
                        FollowButton.Visible = true;
                    }
                    i++;
                }
            }
        }

        protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
        {
            return DragMode.Move;
        }
    }

    [UsedImplicitly, AnyCommand]
    public sealed class VoteMenuCommand : IConsoleCommand
    {
        public string Command => "votemenu";
        public string Description => Loc.GetString("ui-vote-menu-command-description");
        public string Help => Loc.GetString("ui-vote-menu-command-help-text");

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            new VoteCallMenu().OpenCentered();
        }
    }

    public record struct CreateVoteOption
    {
        public string Name;
        public List<Dictionary<string, string>> Dropdowns;
        public bool EnableVoteWarning;
        public int? FollowDropdownId;  // If set, this will enable the Follow button and use the dropdown matching the ID as input.

        public CreateVoteOption(string name, List<Dictionary<string, string>> dropdowns, bool enableVoteWarning, int? followDropdownId)
        {
            Name = name;
            Dropdowns = dropdowns;
            EnableVoteWarning = enableVoteWarning;
            FollowDropdownId = followDropdownId;
        }
    }
}
