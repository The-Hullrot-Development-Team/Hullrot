using System.Numerics;
using Content.Client.Examine;
using Content.Client.Hands.Systems;
using Content.Client.Interaction;
using Content.Client.Storage.Systems;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Client.UserInterface.Systems.Storage.Controls;
using Content.Client.Verbs.UI;
using Content.Shared.CCVar;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Storage;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.Storage;

public sealed class StorageUIController : UIController, IOnSystemChanged<StorageSystem>
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    private HandsSystem? _hands;

    private readonly DragDropHelper<ItemGridPiece> _menuDragHelper;
    private StorageContainer? _container;

    private Vector2? _lastContainerPosition;

    private HotbarGui? Hotbar => UIManager.GetActiveUIWidgetOrNull<HotbarGui>();

    public ItemGridPiece? DraggingGhost;
    public Angle DraggingRotation = Angle.Zero;
    public bool StaticStorageUIEnabled;

    public bool IsDragging => _menuDragHelper.IsDragging;
    public ItemGridPiece? CurrentlyDragging => _menuDragHelper.Dragged;

    public StorageUIController()
    {
        _menuDragHelper = new DragDropHelper<ItemGridPiece>(OnMenuBeginDrag, OnMenuContinueDrag, OnMenuEndDrag);
    }

    public override void Initialize()
    {
        base.Initialize();

        _configuration.OnValueChanged(CCVars.StaticStorageUI, OnStaticStorageChanged, true);

        //EntityManager.EventBus.SubscribeLocalEvent<StorageComponent, ComponentShutdown>(OnStorageShutdown);
    }

    private void OnStorageShutdown(EntityUid uid, StorageComponent component, ComponentShutdown args)
    {
        //todo: close the storage window nerd
    }

    public void OnSystemLoaded(StorageSystem system)
    {
        _input.FirstChanceOnKeyEvent += OnMiddleMouse;
        system.StorageUpdated += OnStorageUpdated;
        system.StorageOrderChanged += OnStorageOrderChanged;
    }

    public void OnSystemUnloaded(StorageSystem system)
    {
        _input.FirstChanceOnKeyEvent -= OnMiddleMouse;
        system.StorageUpdated -= OnStorageUpdated;
        system.StorageOrderChanged -= OnStorageOrderChanged;
    }

    private void OnStorageOrderChanged(Entity<StorageComponent>? nullEnt)
    {
        if (_container == null)
            return;

        _container.UpdateContainer(nullEnt);

        if (nullEnt is { } ent)
        {
            if (_lastContainerPosition == null)
                _container.OpenCenteredAt(new Vector2(0.5f, 0.75f));
            else
            {
                _container.Open();

                if (!StaticStorageUIEnabled)
                    LayoutContainer.SetPosition(_container, _lastContainerPosition.Value);
            }

            if (StaticStorageUIEnabled)
            {
                // we have to orphan it here because Open() sets the parent.
                _container.Orphan();
                Hotbar?.StorageContainer.AddChild(_container);
            }
        }
        else
        {
            _lastContainerPosition = _container.GlobalPosition;
            _container.Close();
        }
    }

    private void OnStaticStorageChanged(bool obj)
    {
        if (StaticStorageUIEnabled == obj)
            return;

        StaticStorageUIEnabled = obj;

        if (_container == null)
            return;

        if (!_container.IsOpen)
            return;

        _container.Orphan();
        if (StaticStorageUIEnabled)
        {
            Hotbar?.StorageContainer.AddChild(_container);
            _lastContainerPosition = null;
        }
        else
        {
            _ui.WindowRoot.AddChild(_container);
        }
    }

    /// One might ask, Hey Emo, why are you parsing raw keyboard input just to rotate a rectangle?
    /// The answer is, that input bindings regarding mouse inputs are always intercepted by the UI,
    /// thus, if i want to be able to rotate my damn piece anywhere on the screen,
    /// I have to sidestep all of the input handling. Cheers.
    private void OnMiddleMouse(KeyEventArgs keyEvent, KeyEventType type)
    {
        if (keyEvent.Handled)
            return;

        _hands ??= _entity.System<HandsSystem>();
        if (!IsDragging && _hands.GetActiveHandEntity() == null)
            return;

        if (type != KeyEventType.Down)
            return;

        //todo there's gotta be a method for this in InputManager just expose it to content I BEG.
        if (!_input.TryGetKeyBinding(ContentKeyFunctions.RotateStoredItem, out var binding))
            return;
        if (binding.BaseKey != keyEvent.Key)
            return;

        if (keyEvent.Shift &&
            !(binding.Mod1 == Keyboard.Key.Shift ||
              binding.Mod2 == Keyboard.Key.Shift ||
              binding.Mod3 == Keyboard.Key.Shift))
            return;

        if (keyEvent.Alt &&
            !(binding.Mod1 == Keyboard.Key.Alt ||
              binding.Mod2 == Keyboard.Key.Alt ||
              binding.Mod3 == Keyboard.Key.Alt))
            return;

        if (keyEvent.Control &&
            !(binding.Mod1 == Keyboard.Key.Control ||
              binding.Mod2 == Keyboard.Key.Control ||
              binding.Mod3 == Keyboard.Key.Control))
            return;

        //clamp it to a cardinal.
        DraggingRotation = (DraggingRotation + Math.PI / 2f).GetCardinalDir().ToAngle();
        if (DraggingGhost != null)
            DraggingGhost.Location.Rotation = DraggingRotation;

        if (IsDragging || (_container != null && UIManager.CurrentlyHovered == _container))
            keyEvent.Handle();
    }

    private void OnStorageUpdated(Entity<StorageComponent> uid)
    {
        if (_container?.StorageEntity != uid)
            return;

        _container.BuildItemPieces();
    }

    public void RegisterStorageContainer(StorageContainer container)
    {
        if (_container != null)
        {
            container.OnPiecePressed -= OnPiecePressed;
            container.OnPieceUnpressed -= OnPieceUnpressed;
        }

        _container = container;
        container.OnPiecePressed += OnPiecePressed;
        container.OnPieceUnpressed += OnPieceUnpressed;
    }

    private void OnPiecePressed(GUIBoundKeyEventArgs args, ItemGridPiece control)
    {
        if (IsDragging || !_container?.IsOpen == true)
            return;

        if (args.Function == ContentKeyFunctions.MoveStoredItem)
        {
            _menuDragHelper.MouseDown(control);
            _menuDragHelper.Update(0f);

            args.Handle();
        }
        else if (args.Function == ContentKeyFunctions.ExamineEntity)
        {
            _entity.System<ExamineSystem>().DoExamine(control.Entity);
            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.UseSecondary)
        {
            UIManager.GetUIController<VerbMenuUIController>().OpenVerbMenu(control.Entity);
            args.Handle();
        }
        else if (args.Function == ContentKeyFunctions.ActivateItemInWorld)
        {
            _entity.EntityNetManager?.SendSystemNetworkMessage(
                new InteractInventorySlotEvent(_entity.GetNetEntity(control.Entity), altInteract: false));
            args.Handle();
        }
        else if (args.Function == ContentKeyFunctions.AltActivateItemInWorld)
        {
            _entity.RaisePredictiveEvent(new InteractInventorySlotEvent(_entity.GetNetEntity(control.Entity), altInteract: true));
            args.Handle();
        }
    }

    private void OnPieceUnpressed(GUIBoundKeyEventArgs args, ItemGridPiece control)
    {
        if (_container?.StorageEntity is not { } storageEnt)
            return;

        if (args.Function == ContentKeyFunctions.MoveStoredItem)
        {
            if (DraggingGhost is { } draggingGhost)
            {
                var position = _container.GetMouseGridPieceLocation(draggingGhost.Entity, draggingGhost.Location);
                _entity.RaisePredictiveEvent(new StorageSetItemLocationEvent(
                    _entity.GetNetEntity(draggingGhost.Entity),
                    _entity.GetNetEntity(storageEnt),
                    new ItemStorageLocation(DraggingRotation, position)));
                _container?.BuildItemPieces();
            }
            else //if we just clicked, then take it out of the bag.
            {
                _entity.RaisePredictiveEvent(new StorageInteractWithItemEvent(
                    _entity.GetNetEntity(control.Entity),
                    _entity.GetNetEntity(storageEnt)));
            }
            _menuDragHelper.EndDrag();
            args.Handle();
        }
    }

    private bool OnMenuBeginDrag()
    {
        if (_menuDragHelper.Dragged is not { } dragged)
            return false;

        DraggingRotation = dragged.Location.Rotation;
        DraggingGhost = new ItemGridPiece(
            (dragged.Entity, _entity.GetComponent<ItemComponent>(dragged.Entity)),
            dragged.Location,
            _entity);
        DraggingGhost.MouseFilter = Control.MouseFilterMode.Ignore;
        DraggingGhost.Visible = true;
        DraggingGhost.Orphan();

        UIManager.PopupRoot.AddChild(DraggingGhost);
        SetDraggingRotation();
        return true;
    }

    private bool OnMenuContinueDrag(float frameTime)
    {
        if (DraggingGhost == null)
            return false;
        SetDraggingRotation();
        return true;
    }

    private void SetDraggingRotation()
    {
        if (DraggingGhost == null)
            return;

        var offset = ItemGridPiece.GetCenterOffset(
            (DraggingGhost.Entity, null),
            new ItemStorageLocation(DraggingRotation, Vector2i.Zero),
            _entity);

        // I don't know why it divides the position by 2. Hope this helps! -emo
        LayoutContainer.SetPosition(DraggingGhost, UIManager.MousePositionScaled.Position / 2 - offset );
    }

    private void OnMenuEndDrag()
    {
        if (DraggingGhost == null)
            return;
        DraggingGhost.Visible = false;
        DraggingGhost = null;
        DraggingRotation = Angle.Zero;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _menuDragHelper.Update(args.DeltaSeconds);
    }
}
