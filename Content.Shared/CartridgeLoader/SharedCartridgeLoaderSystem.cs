﻿using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.CartridgeLoader;

public abstract class SharedCartridgeLoaderSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SharedCartridgeLoaderComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<SharedCartridgeLoaderComponent, ComponentRemove>(OnComponentRemove);

        SubscribeLocalEvent<SharedCartridgeLoaderComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<SharedCartridgeLoaderComponent, EntRemovedFromContainerMessage>(OnItemRemoved);

        SubscribeLocalEvent<CartridgeComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<CartridgeComponent, ComponentHandleState>(OnHandleState);

    }

    private void OnComponentInit(EntityUid uid, SharedCartridgeLoaderComponent loader, ComponentInit args)
    {
        _itemSlotsSystem.AddItemSlot(uid, SharedCartridgeLoaderComponent.CartridgeSlotId, loader.CartridgeSlot);
    }

    private void OnComponentRemove(EntityUid uid, SharedCartridgeLoaderComponent loader, ComponentRemove args)
    {
        _itemSlotsSystem.RemoveItemSlot(uid, loader.CartridgeSlot);

        foreach (var program in loader.InstalledPrograms)
        {
               EntityManager.QueueDeleteEntity(program);
        }
    }

    protected virtual void OnItemInserted(EntityUid uid, SharedCartridgeLoaderComponent loader, EntInsertedIntoContainerMessage args)
    {
        UpdateAppearanceData(uid, loader);
    }

    protected virtual void OnItemRemoved(EntityUid uid, SharedCartridgeLoaderComponent loader, EntRemovedFromContainerMessage args)
    {
        UpdateAppearanceData(uid, loader);
    }

    private void OnGetState(EntityUid uid, CartridgeComponent component, ref ComponentGetState args)
    {
        var state = new CartridgeComponentState();
        state.InstallationStatus = component.InstallationStatus;

        args.State = state;
    }

    private void OnHandleState(EntityUid uid, CartridgeComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not CartridgeComponentState state)
            return;

        component.InstallationStatus = state.InstallationStatus;
    }

    private void UpdateAppearanceData(EntityUid uid, SharedCartridgeLoaderComponent loader)
    {
        _appearanceSystem.SetData(uid, CartridgeLoaderVisuals.CartridgeInserted, loader.CartridgeSlot.HasItem);
    }
}

public sealed class CartridgeAddedEvent : EntityEventArgs
{
    public readonly EntityUid Loader;

    public CartridgeAddedEvent(EntityUid loader)
    {
        Loader = loader;
    }
}

public sealed class CartridgeRemovedEvent : EntityEventArgs
{
    public readonly EntityUid Loader;

    public CartridgeRemovedEvent(EntityUid loader)
    {
        Loader = loader;
    }
}

public sealed class CartridgeActivatedEvent : EntityEventArgs
{
    public readonly EntityUid Loader;

    public CartridgeActivatedEvent(EntityUid loader)
    {
        Loader = loader;
    }
}

public sealed class CartridgeDeactivatedEvent : EntityEventArgs
{
    public readonly EntityUid Loader;

    public CartridgeDeactivatedEvent(EntityUid loader)
    {
        Loader = loader;
    }
}

public sealed class CartridgeUiReadyEvent : EntityEventArgs
{
    public readonly EntityUid Loader;

    public CartridgeUiReadyEvent(EntityUid loader)
    {
        Loader = loader;
    }
}
