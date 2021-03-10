#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.GameObjects.Components.Items;
using Content.Client.Utility;
using Content.Shared.GameObjects.Components.Items;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using Robust.Shared.ViewVariables;

namespace Content.Client.UserInterface
{
    public class HandsGui : Control
    {
        [Dependency] private readonly IResourceCache _resourceCache = default!;
        [Dependency] private readonly IItemSlotManager _itemSlotManager = default!;

        private Texture LeftHandTexture { get; }
        private Texture MiddleHandTexture { get; }
        private Texture RightHandTexture { get; }
        private Texture StorageTexture { get; }
        private Texture BlockedTexture { get; }

        private ItemStatusPanel LeftPanel { get; }
        private ItemStatusPanel TopPanel { get; }
        private ItemStatusPanel RightPanel { get; }

        private HBoxContainer HandsContainer { get; }


        [ViewVariables]
        private List<GuiHand> Hands { get; set; } = new();

        /// <summary>
        ///     The hands component that created this. Should only be used for sending network messages.
        /// </summary>
        private HandsComponent Creator { get; set; }

        private int? ActiveHand { get; set; } = null;

        public HandsGui(HandsComponent creator)
        {
            Creator = creator;
            IoCManager.InjectDependencies(this);
            AddChild(new VBoxContainer
            {
                SeparationOverride = 0,
                Children =
                {
                    (TopPanel = ItemStatusPanel.FromSide(HandLocation.Middle)),
                    new HBoxContainer
                    {
                        Children =
                        {
                            (LeftPanel = ItemStatusPanel.FromSide(HandLocation.Left)),
                            (HandsContainer = new HBoxContainer()),
                            (RightPanel = ItemStatusPanel.FromSide(HandLocation.Right))
                        }
                    },
                }
            });
            LeftHandTexture = _resourceCache.GetTexture("/Textures/Interface/Inventory/hand_l.png");
            MiddleHandTexture = _resourceCache.GetTexture("/Textures/Interface/Inventory/hand_l.png");
            RightHandTexture = _resourceCache.GetTexture("/Textures/Interface/Inventory/hand_r.png");
            StorageTexture = _resourceCache.GetTexture("/Textures/Interface/Inventory/back.png");
            BlockedTexture = _resourceCache.GetTexture("/Textures/Interface/Inventory/blocked.png");

            foreach (HandLocation location in Enum.GetValues(typeof(HandLocation)))
            {
                GetStatusPanel(location).Update(null);
            }
        }

        public void SetState(HandsGuiState state)
        {
            ActiveHand = state.ActiveHand;
            Hands = state.GuiHands;
            UpdateGui();
        }

        private void UpdateGui()
        {
            HandsContainer.DisposeAllChildren();

            foreach (var hand in Hands)
            {
                var location = hand.HandLocation;
                var heldItem = hand.HeldItem;

                var newButton = MakeHandButton(location);
                HandsContainer.AddChild(newButton);
                hand.HandButton = newButton;

                GetStatusPanel(location).Update(heldItem);
                _itemSlotManager.SetItemSlot(newButton, heldItem);
            }
            if (ActiveHand != null)
            {
                if (TryGetHandButton(ActiveHand.Value, out var handButton))
                {
                    handButton.SetActiveHand(true);
                }
            }
        }

        private bool TryGetHandButton(int handNumber, [NotNullWhen(true)] out HandButton? handButton)
        {
            handButton = Hands.ElementAtOrDefault(handNumber)?.HandButton;
            return handButton != null;
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            foreach (var hand in Hands)
                _itemSlotManager.UpdateCooldown(hand.HandButton, hand.HeldItem);
        }

        private ItemStatusPanel GetStatusPanel(HandLocation handLocation)
        {
            return handLocation switch
            {
                HandLocation.Left => RightPanel, //The player's left hand is the rightmost panel
                HandLocation.Middle => TopPanel,
                HandLocation.Right => LeftPanel, //The player's right hand is the leftmost panel
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private HandButton MakeHandButton(HandLocation buttonLocation)
        {
            var buttonTexture = buttonLocation switch
            {
                HandLocation.Left => LeftHandTexture,
                HandLocation.Middle => MiddleHandTexture,
                HandLocation.Right => RightHandTexture,
                _ => throw new ArgumentOutOfRangeException()
            };
            return new HandButton(buttonTexture, StorageTexture, BlockedTexture, buttonLocation);
        }
    }

    /// <summary>
    ///     Info on a set of hands to be displayed.
    /// </summary>
    public class HandsGuiState
    {
        /// <summary>
        ///     The set of hands to be displayed.
        /// </summary>
        [ViewVariables]
        public List<GuiHand> GuiHands { get; } = new();

        /// <summary>
        ///     The name of the hand that is currently selected by this player.
        /// </summary>
        [ViewVariables]
        public int? ActiveHand { get; }

        public HandsGuiState() { }

        public HandsGuiState(List<GuiHand> guiHands, int? activeHand)
        {
            GuiHands = guiHands;
            ActiveHand = activeHand;
        }
    }

    /// <summary>
    ///     Info on an individual hand to be displayed.
    /// </summary>
    public class GuiHand
    {
        /// <summary>
        ///     The name of this hand.
        /// </summary>
        [ViewVariables]
        public string Name { get; }

        /// <summary>
        ///     Where this hand is located.
        /// </summary>
        [ViewVariables]
        public HandLocation HandLocation { get; }

        /// <summary>
        ///     The item being held in this hand.
        /// </summary>
        [ViewVariables]
        public IEntity? HeldItem { get; }

        [ViewVariables]
        public HandButton HandButton { get; set; } = default!;

        public GuiHand(string name, HandLocation handLocation, IEntity? heldItem)
        {
            Name = name;
            HandLocation = handLocation;
            HeldItem = heldItem;
        }
    }
}
