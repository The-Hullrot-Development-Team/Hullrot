using System.Linq;
using Content.Client.Computer;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Procedural.Loot;
using Content.Shared.Salvage;
using Content.Shared.Salvage.Expeditions.Modifiers;
using Content.Shared.Shuttles.BUIStates;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Salvage.UI;

[GenerateTypedNameReferences]
public sealed partial class SalvageExpeditionWindow : FancyWindow,
    IComputerWindow<EmergencyConsoleBoundUserInterfaceState>
{
    private readonly IGameTiming _timing;
    private readonly IPrototypeManager _prototype;
    private readonly SharedSalvageSystem _salvage;

    public event Action<ushort>? ClaimMission;
    private bool _claimed;
    private bool _cooldown;
    private TimeSpan _nextOffer;

    public SalvageExpeditionWindow()
    {
        RobustXamlLoader.Load(this);
        _timing = IoCManager.Resolve<IGameTiming>();
        _prototype = IoCManager.Resolve<IPrototypeManager>();
        _salvage = IoCManager.Resolve<IEntityManager>().EntitySysManager.GetEntitySystem<SharedSalvageSystem>();
    }

    public void UpdateState(SalvageExpeditionConsoleState state)
    {
        _claimed = state.Claimed;
        _cooldown = state.Cooldown;
        _nextOffer = state.NextOffer;
        Container.DisposeAllChildren();

        for (var i = 0; i < state.Missions.Count; i++)
        {
            var missionParams = state.Missions[i];
            var config = _prototype.Index<SalvageMissionPrototype>(missionParams.Config);
            var mission = _salvage.GetMission(missionParams.Config, missionParams.Difficulty, missionParams.Seed);

            var missionDesc = config.Description;

            // Mission title
            var missionStripe = new StripeBack()
            {
                Margin = new Thickness(0f, -5f, 0f, 0f)
            };

            missionStripe.AddChild(new Label()
            {
                Text = missionDesc,
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(0f, 5f, 0f, 5f),
            });

            var lBox = new BoxContainer()
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical
            };

            // Difficulty
            // Details
            lBox.AddChild(new Label()
            {
                Text = Loc.GetString("salvage-expedition-window-difficulty")
            });

            Color difficultyColor;

            switch (missionParams.Difficulty)
            {
                case DifficultyRating.None:
                    difficultyColor = Color.FromHex("#52B4E996");
                    break;
                case DifficultyRating.Minor:
                    difficultyColor = Color.FromHex("#9FED5896");
                    break;
                case DifficultyRating.Moderate:
                    difficultyColor = Color.FromHex("#EFB34196");
                    break;
                case DifficultyRating.Hazardous:
                    difficultyColor = Color.FromHex("#DE3A3A96");
                    break;
                case DifficultyRating.Extreme:
                    difficultyColor = Color.FromHex("#D381C996");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            lBox.AddChild(new Label
            {
                Text = missionParams.Difficulty.ToString(),
                FontColorOverride = difficultyColor,
                HorizontalAlignment = HAlignment.Left,
                Margin = new Thickness(0f, 0f, 0f, 5f),
            });

            // Details
            var details = _salvage.GetMissionDescription(mission);

            lBox.AddChild(new Label
            {
                Text = Loc.GetString("salvage-expedition-window-details")
            });

            lBox.AddChild(new Label
            {
                Text = details,
                FontColorOverride = StyleNano.NanoGold,
                HorizontalAlignment = HAlignment.Left,
                Margin = new Thickness(0f, 0f, 0f, 5f),
            });

            // Details
            lBox.AddChild(new Label
            {
                Text = Loc.GetString("salvage-expedition-window-hostiles")
            });

            var faction = mission.Faction;

            lBox.AddChild(new Label
            {
                Text = faction,
                FontColorOverride = StyleNano.NanoGold,
                HorizontalAlignment = HAlignment.Left,
                Margin = new Thickness(0f, 0f, 0f, 5f),
            });

            // Duration
            lBox.AddChild(new Label
            {
                Text = Loc.GetString("salvage-expedition-window-duration")
            });

            lBox.AddChild(new Label
            {
                Text = mission.Duration.ToString(),
                FontColorOverride = StyleNano.NanoGold,
                HorizontalAlignment = HAlignment.Left,
                Margin = new Thickness(0f, 0f, 0f, 5f),
            });

            // Biome
            lBox.AddChild(new Label
            {
                Text = Loc.GetString("salvage-expedition-window-biome")
            });

            var biome = mission.Biome;

            lBox.AddChild(new Label
            {
                Text = Loc.GetString(_prototype.Index<SalvageBiomeMod>(biome).ID),
                FontColorOverride = StyleNano.NanoGold,
                HorizontalAlignment = HAlignment.Left,
                Margin = new Thickness(0f, 0f, 0f, 5f),
            });

            // Modifiers
            lBox.AddChild(new Label
            {
                Text = Loc.GetString("salvage-expedition-window-modifiers")
            });

            var mods = mission.Modifiers;

            lBox.AddChild(new Label
            {
                Text = string.Join("\n", mods.Select(o => "- " + o)).TrimEnd(),
                FontColorOverride = StyleNano.NanoGold,
                HorizontalAlignment = HAlignment.Left,
                Margin = new Thickness(0f, 0f, 0f, 5f),
            });

            lBox.AddChild(new Label()
            {
                Text = Loc.GetString("salvage-expedition-window-loot")
            });

            if (mission.Loot.Count == 0)
            {
                lBox.AddChild(new Label()
                {
                    Text = Loc.GetString("salvage-expedition-window-none"),
                    FontColorOverride = StyleNano.ConcerningOrangeFore,
                    HorizontalAlignment = HAlignment.Left,
                    Margin = new Thickness(0f, 0f, 0f, 5f),
                });
            }
            else
            {
                lBox.AddChild(new Label()
                {
                    Text = string.Join("\n", mission.Loot.Select(o => "- " + _prototype.Index<SalvageLootPrototype>(o.Key).Description + (o.Value > 1 ? $" x {o.Value}" : ""))).TrimEnd(),
                    FontColorOverride = StyleNano.ConcerningOrangeFore,
                    HorizontalAlignment = HAlignment.Left,
                    Margin = new Thickness(0f, 0f, 0f, 5f),
                });
            }

            // Claim
            var claimButton = new Button()
            {
                HorizontalExpand = true,
                VerticalAlignment = VAlignment.Bottom,
                Pressed = state.ActiveMission == missionParams.Index,
                ToggleMode = true,
                Disabled = state.Claimed || state.Cooldown,
            };

            claimButton.Label.Margin = new Thickness(0f, 5f);

            claimButton.OnPressed += args =>
            {
                ClaimMission?.Invoke(missionParams.Index);
            };

            if (state.ActiveMission == missionParams.Index)
            {
                claimButton.Text = Loc.GetString("salvage-expedition-window-claimed");
                claimButton.AddStyleClass(StyleBase.ButtonCaution);
            }
            else
            {
                claimButton.Text = Loc.GetString("salvage-expedition-window-claim");
            }

            var box = new PanelContainer
            {
                PanelOverride = new StyleBoxFlat(new Color(30, 30, 34)),
                HorizontalExpand = true,
                Margin = new Thickness(5f, 0f),
                Children =
                {
                    new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Vertical,
                        Children =
                        {
                            missionStripe,
                            lBox,
                            new Control() {VerticalExpand = true},
                            claimButton,
                        },
                        Margin = new Thickness(5f, 5f)
                    }
                }
            };

            LayoutContainer.SetAnchorPreset(box, LayoutContainer.LayoutPreset.Wide);

            Container.AddChild(box);
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_claimed)
        {
            NextOfferBar.Value = 0f;
            NextOfferText.Text = "00:00";
            return;
        }

        var remaining = _nextOffer - _timing.CurTime;

        if (remaining < TimeSpan.Zero)
        {
            NextOfferBar.Value = 1f;
            NextOfferText.Text = "00:00";
        }
        else
        {
            var cooldown = _cooldown
                ? SharedSalvageSystem.MissionFailedCooldown
                : SharedSalvageSystem.MissionCooldown;

            NextOfferBar.Value = 1f - (float) (remaining / cooldown);
            NextOfferText.Text = $"{remaining.Minutes:00}:{remaining.Seconds:00}";
        }
    }
}
