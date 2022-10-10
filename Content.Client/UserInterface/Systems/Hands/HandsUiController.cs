﻿using Content.Client.Gameplay;
using Content.Client.Hands;
using Content.Client.Hands.Systems;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Hands.Controls;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Shared.Hands.Components;
using Content.Shared.Input;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Hands;

public sealed class HandsUIController : UIController, IOnStateEntered<GameplayState>, IOnSystemChanged<HandsSystem>
{
    [Dependency] private readonly IEntityManager _entities = default!;

    [UISystemDependency] private readonly HandsSystem _handsSystem = default!;

    private readonly List<HandsContainer> _handsContainers = new();
    private readonly Dictionary<string, int> _handContainerIndices = new();
    private readonly Dictionary<string, HandButton> _handLookup = new();
    private HandsComponent? _playerHandsComponent;
    private HandButton? _activeHand = null;
    private int _backupSuffix = 0; //this is used when autogenerating container names if they don't have names

    private HotbarGui? HandsGui => UIManager.GetActiveUIWidgetOrNull<HotbarGui>();

    public void OnSystemLoaded(HandsSystem system)
    {
        _handsSystem.OnAddHand += AddHand;
        _handsSystem.OnItemAdded += OnItemAdded;
        _handsSystem.OnItemRemoved += OnItemRemoved;
        _handsSystem.OnSetActiveHand += SetActiveHand;
        _handsSystem.OnRemoveHand += RemoveHand;
        _handsSystem.OnPlayerHandsAdded += LoadPlayerHands;
        _handsSystem.OnPlayerHandsRemoved += UnloadPlayerHands;
        _handsSystem.OnHandBlocked += HandBlocked;
        _handsSystem.OnHandUnblocked += HandUnblocked;
    }

    public void OnSystemUnloaded(HandsSystem system)
    {
        _handsSystem.OnAddHand -= AddHand;
        _handsSystem.OnItemAdded -= OnItemAdded;
        _handsSystem.OnItemRemoved -= OnItemRemoved;
        _handsSystem.OnSetActiveHand -= SetActiveHand;
        _handsSystem.OnRemoveHand -= RemoveHand;
        _handsSystem.OnPlayerHandsAdded -= LoadPlayerHands;
        _handsSystem.OnPlayerHandsRemoved -= UnloadPlayerHands;
        _handsSystem.OnHandBlocked -= HandBlocked;
        _handsSystem.OnHandUnblocked -= HandUnblocked;
    }

    private void HandPressed(GUIBoundKeyEventArgs args, SlotControl hand)
    {
        if (_playerHandsComponent == null)
        {
            return;
        }

        if (args.Function == ContentKeyFunctions.ExamineEntity)
        {
            _handsSystem.UIInventoryExamine(hand.SlotName);
        }
        else if (args.Function == EngineKeyFunctions.UseSecondary)
        {
            _handsSystem.UIHandOpenContextMenu(hand.SlotName);
        }
        else if (args.Function == EngineKeyFunctions.UIClick)
        {
            _handsSystem.UIHandClick(_playerHandsComponent, hand.SlotName);
        }
    }

    private void UnloadPlayerHands()
    {
        if (HandsGui != null)
            HandsGui.Visible = false;

        _handContainerIndices.Clear();
        _handLookup.Clear();
        _playerHandsComponent = null;

        foreach (var container in _handsContainers)
        {
            container.Clear();
        }
    }

    private void LoadPlayerHands(HandsComponent handsComp)
    {
        DebugTools.Assert(_playerHandsComponent == null);
        if (HandsGui != null)
            HandsGui.Visible = true;

        _playerHandsComponent = handsComp;
        foreach (var (name, hand) in handsComp.Hands)
        {
            AddHand(name, hand.Location);
        }

        var activeHand = handsComp.ActiveHand;
        if (activeHand == null)
            return;
        SetActiveHand(activeHand.Name);
    }

    private void HandBlocked(string handName)
    {
        if (!_handLookup.TryGetValue(handName, out var hand))
        {
            return;
        }

        hand.Blocked = true;
    }

    private void HandUnblocked(string handName)
    {
        if (!_handLookup.TryGetValue(handName, out var hand))
        {
            return;
        }

        hand.Blocked = false;
    }

    private int GetHandContainerIndex(string containerName)
    {
        if (!_handContainerIndices.TryGetValue(containerName, out var result))
            return -1;
        return result;
    }

    private void OnItemAdded(string name, EntityUid entity)
    {
        HandsGui?.UpdatePanelEntity(entity);
        var hand = GetHand(name);
        if (hand == null)
            return;
        if (_entities.TryGetComponent(entity, out ISpriteComponent? sprite))
        {
            hand.SpriteView.Sprite = sprite;
        }
    }

    private void OnItemRemoved(string name, EntityUid entity)
    {
        HandsGui?.UpdatePanelEntity(null);
        var hand = GetHand(name);
        if (hand == null)
            return;
        hand.SpriteView.Sprite = null;
    }

    private HandsContainer GetFirstAvailableContainer()
    {
        if (_handsContainers.Count == 0)
            throw new Exception("Could not find an attached hand hud container");
        foreach (var container in _handsContainers)
        {
            if (container.IsFull)
                continue;
            return container;
        }

        throw new Exception("All attached hand hud containers were full!");
    }

    public bool TryGetHandContainer(string containerName, out HandsContainer? container)
    {
        container = null;
        var containerIndex = GetHandContainerIndex(containerName);
        if (containerIndex == -1)
            return false;
        container = _handsContainers[containerIndex];
        return true;
    }

    //propagate hand activation to the hand system.
    private void StorageActivate(GUIBoundKeyEventArgs args, SlotControl handControl)
    {
        _handsSystem.UIHandActivate(handControl.SlotName);
    }

    private void SetActiveHand(string? handName)
    {
        if (handName == null)
        {
            if (_activeHand != null)
                _activeHand.Highlight = false;

            HandsGui?.UpdatePanelEntity(null);
            return;
        }

        if (!_handLookup.TryGetValue(handName, out var handControl) || handControl == _activeHand)
            return;

        if (_activeHand != null)
            _activeHand.Highlight = false;

        handControl.Highlight = true;
        _activeHand = handControl;

        if (HandsGui != null &&
            _playerHandsComponent != null &&
            _playerHandsComponent.Hands.TryGetValue(handName, out var hand))
        {
            HandsGui.UpdatePanelEntity(hand.HeldEntity);
        }
    }

    private HandButton? GetHand(string handName)
    {
        _handLookup.TryGetValue(handName, out var handControl);
        return handControl;
    }

    private void AddHand(string handName, HandLocation location)
    {
        var newHandButton = new HandButton(handName, location);
        newHandButton.StoragePressed += StorageActivate;
        newHandButton.Pressed += HandPressed;
        if (!_handLookup.TryAdd(handName, newHandButton))
            throw new Exception("Tried to add hand with duplicate name to UI. Name:" + handName);
        GetFirstAvailableContainer().AddButton(newHandButton);
    }

    private void RemoveHand(string handName)
    {
        RemoveHand(handName, out _);
    }

    private bool RemoveHand(string handName, out HandButton? handButton)
    {
        handButton = null;
        if (!_handLookup.TryGetValue(handName, out handButton))
            return false;
        if (handButton.Parent is HandsContainer handContainer)
        {
            handContainer.RemoveButton(handButton);
        }

        _handLookup.Remove(handName);
        handButton.Dispose();
        return true;
    }

    public string RegisterHandContainer(HandsContainer handContainer)
    {
        var name = "HandContainer_" + _backupSuffix;
        ;
        if (handContainer.Name == null)
        {
            handContainer.Name = name;
            _backupSuffix++;
        }
        else
        {
            name = handContainer.Name;
        }

        _handContainerIndices.Add(name, _handsContainers.Count);
        _handsContainers.Add(handContainer);
        return name;
    }

    public bool RemoveHandContainer(string handContainerName)
    {
        var index = GetHandContainerIndex(handContainerName);
        if (index == -1)
            return false;
        _handContainerIndices.Remove(handContainerName);
        _handsContainers.RemoveAt(index);
        return true;
    }

    public bool RemoveHandContainer(string handContainerName, out HandsContainer? container)
    {
        var success = _handContainerIndices.TryGetValue(handContainerName, out var index);
        container = _handsContainers[index];
        _handContainerIndices.Remove(handContainerName);
        _handsContainers.RemoveAt(index);
        return success;
    }

    public void OnStateEntered(GameplayState state)
    {
        if (HandsGui != null)
            HandsGui.Visible = _playerHandsComponent != null;
    }
}
