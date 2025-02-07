﻿using System.ComponentModel;
using System.Runtime.CompilerServices;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using FancyWindow = Content.Client.UserInterface.Controls.FancyWindow;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Timing;
using Content.Shared.Kitchen.Components;
using TerraFX.Interop.Xlib;

namespace Content.Client.Kitchen.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class MicrowaveMenu : FancyWindow
    {
        [Robust.Shared.IoC.Dependency] private readonly IGameTiming _timing = default!;
        [Robust.Shared.IoC.Dependency] private readonly IEntityManager _entityManager = default!;
        public event Action<BaseButton.ButtonEventArgs, int>? OnCookTimeSelected;

        public ButtonGroup CookTimeButtonGroup { get; }

        public bool IsBusy;
        public TimeSpan CurrentCooktimeEnd;

        public MicrowaveMenu()
        {
            IoCManager.InjectDependencies(this);
            RobustXamlLoader.Load(this);

            CookTimeButtonGroup = new ButtonGroup();
            InstantCookButton.Group = CookTimeButtonGroup;
            InstantCookButton.OnPressed += args =>
            {
                OnCookTimeSelected?.Invoke(args, 0);
            };
        }

        private EntityUid _owner;
        public void SetOwner(EntityUid ent)
        {
            _owner = ent;
            Refresh();
        }

        public void Refresh()
        {
            _entityManager.TryGetComponent<MicrowaveButtonsComponent>(_owner, out var buttons);

            var numberButtons = (buttons != null ? buttons.NumberOfButtons : 6);

            if (numberButtons == 12)
            {
                Title = Loc.GetString("microwave-menu-title",
                        ("title", "Oven"));
            }
            else
            {
                Title = Loc.GetString("microwave-menu-title",
                        ("title", "Microwave"));
            }

            for (var i = 1; i <= numberButtons; i++)
            {
                var newButton = new MicrowaveCookTimeButton
                {
                    Text = (i * 5).ToString(),
                    TextAlign = Label.AlignMode.Center,
                    ToggleMode = true,
                    CookTime = (uint)(i * 5),
                    Group = CookTimeButtonGroup,
                    HorizontalExpand = true,
                };
                if (i == (numberButtons - 2))
                {
                    newButton.StyleClasses.Add("OpenRight");
                }
                else
                {
                    newButton.StyleClasses.Add("OpenBoth");
                }

                CookTimeButtonVbox.AddChild(newButton);
                newButton.OnPressed += args =>
                {
                    OnCookTimeSelected?.Invoke(args, i);
                };
            }
        }

        public void ToggleBusyDisableOverlayPanel(bool shouldDisable)
        {
            DisableCookingPanelOverlay.Visible = shouldDisable;
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            if (!IsBusy)
                return;

            if (CurrentCooktimeEnd > _timing.CurTime)
            {
                CookTimeInfoLabel.Text = Loc.GetString("microwave-bound-user-interface-cook-time-label",
                ("time", CurrentCooktimeEnd.Subtract(_timing.CurTime).Seconds));
            }
        }

        public sealed class MicrowaveCookTimeButton : Button
        {
            public uint CookTime;
        }
    }
}
