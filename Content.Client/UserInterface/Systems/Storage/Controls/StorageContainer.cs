﻿using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Client.Hands.Systems;
using Content.Client.Items.Systems;
using Content.Client.Storage.Systems;
using Content.Shared.Input;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Storage.Controls;

public sealed class StorageContainer : BaseWindow
{
    [Dependency] private readonly IEntityManager _entity = default!;
    private readonly StorageUIController _storageController;
    private ItemSystem? _itemSystem;
    private StorageSystem? _storageSystem;
    private HandsSystem? _handsSystem;

    public EntityUid? StorageEntity;

    private readonly GridContainer _pieceGrid;
    private readonly GridContainer _backgroundGrid;
    private readonly GridContainer _sidebar;
    private readonly Label _nameLabel;

    public event Action<GUIBoundKeyEventArgs, ItemGridPiece>? OnPiecePressed;
    public event Action<GUIBoundKeyEventArgs, ItemGridPiece>? OnPieceUnpressed;

    private readonly string _emptyTexturePath = "Storage/tile_empty";
    private Texture? _emptyTexture;
    private readonly string _blockedTexturePath = "Storage/tile_blocked";
    private Texture? _blockedTexture;
    private readonly string _exitTexturePath = "Storage/exit";
    private Texture? _exitTexture;
    private readonly string _backTexturePath = "Storage/back";
    private Texture? _backTexture;
    private readonly string _sidebarTopTexturePath = "Storage/sidebar_top";
    private Texture? _sidebarTopTexture;
    private readonly string _sidebarMiddleTexturePath = "Storage/sidebar_mid";
    private Texture? _sidebarMiddleTexture;
    private readonly string _sidebarBottomTexturePath = "Storage/sidebar_bottom";
    private Texture? _sidebarBottomTexture;
    private readonly string _sidebarFatTexturePath = "Storage/sidebar_fat";
    private Texture? _sidebarFatTexture;

    public StorageContainer()
    {
        IoCManager.InjectDependencies(this);

        _storageController = UserInterfaceManager.GetUIController<StorageUIController>();

        OnThemeUpdated();

        MouseFilter = MouseFilterMode.Stop;

        _nameLabel = new Label
        {
            ReservesSpace = true,
            Visible = false,
            HorizontalAlignment = HAlignment.Left
        };

        _sidebar = new GridContainer
        {
            HSeparationOverride = 0,
            VSeparationOverride = 0,
            Columns = 1
        };

        _pieceGrid = new GridContainer
        {
            HSeparationOverride = 0,
            VSeparationOverride = 0
        };

        _backgroundGrid = new GridContainer
        {
            HSeparationOverride = 0,
            VSeparationOverride = 0
        };

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children =
                    {
                        _sidebar,
                        new Control
                        {
                            Children =
                            {
                                _backgroundGrid,
                                _pieceGrid
                            }
                        }
                    }
                },
                _nameLabel
            }
        };

        AddChild(container);
    }

    protected override void OnThemeUpdated()
    {
        base.OnThemeUpdated();

        _emptyTexture = Theme.ResolveTextureOrNull(_emptyTexturePath)?.Texture;
        _blockedTexture = Theme.ResolveTextureOrNull(_blockedTexturePath)?.Texture;
        _exitTexture = Theme.ResolveTextureOrNull(_exitTexturePath)?.Texture;
        _backTexture = Theme.ResolveTextureOrNull(_backTexturePath)?.Texture;
        _sidebarTopTexture = Theme.ResolveTextureOrNull(_sidebarTopTexturePath)?.Texture;
        _sidebarMiddleTexture = Theme.ResolveTextureOrNull(_sidebarMiddleTexturePath)?.Texture;
        _sidebarBottomTexture = Theme.ResolveTextureOrNull(_sidebarBottomTexturePath)?.Texture;
        _sidebarFatTexture = Theme.ResolveTextureOrNull(_sidebarFatTexturePath)?.Texture;
    }

    public void UpdateContainer(Entity<StorageComponent>? entity)
    {
        Visible = entity != null;
        StorageEntity = entity;
        if (entity == null)
            return;

        _nameLabel.Text = _entity.GetComponent<MetaDataComponent>(entity.Value).EntityName;

        BuildGridRepresentation(entity.Value);
    }

    private void BuildGridRepresentation(Entity<StorageComponent> entity)
    {
        var comp = entity.Comp;
        if (!comp.StorageGrid.Any())
            return;

        _storageSystem ??= _entity.System<StorageSystem>();

        var boundingGrid = SharedStorageSystem.GetBoundingBox(comp.StorageGrid);

        _backgroundGrid.Children.Clear();
        _backgroundGrid.Rows = boundingGrid.Height + 1;
        _backgroundGrid.Columns = boundingGrid.Width + 1;
        for (var y = boundingGrid.Bottom; y <= boundingGrid.Top; y++)
        {
            for (var x = boundingGrid.Left; x <= boundingGrid.Right; x++)
            {
                var texture = comp.StorageGrid.Any(g => g.Contains(x, y))
                    ? _emptyTexture
                    : _blockedTexture;

                _backgroundGrid.AddChild(new TextureRect
                {
                    Texture = texture,
                    TextureScale = new Vector2(2, 2)
                });
            }
        }

        #region Sidebar
        _sidebar.Children.Clear();
        _sidebar.Rows = boundingGrid.Height + 1;
        //todo this should change when there is a parent container to return to.
        var exitButton = new TextureButton
        {
            TextureNormal = _storageSystem.OpenStorageAmount == 1
                ?_exitTexture
                : _backTexture,
            Scale = new Vector2(2, 2),
        };
        exitButton.OnPressed += _ =>
        {
            Close();
        };
        var exitContainer = new BoxContainer
        {
            Children =
            {
                new TextureRect
                {
                    Texture = boundingGrid.Height != 0
                        ? _sidebarTopTexture
                        : _sidebarFatTexture,
                    TextureScale = new Vector2(2, 2),
                    Children =
                    {
                        exitButton
                    }
                }
            }
        };
        _sidebar.AddChild(exitContainer);
        for (var i = 0; i < boundingGrid.Height - 1; i++)
        {
            _sidebar.AddChild(new TextureRect
            {
                Texture = _sidebarMiddleTexture,
                TextureScale = new Vector2(2, 2),
            });
        }

        if (boundingGrid.Height > 0)
        {
            _sidebar.AddChild(new TextureRect
            {
                Texture = _sidebarBottomTexture,
                TextureScale = new Vector2(2, 2),
            });
        }

        #endregion

        BuildItemPieces();
    }

    public void BuildItemPieces()
    {
        if (!_entity.TryGetComponent<StorageComponent>(StorageEntity, out var storageComp))
            return;

        if (!storageComp.StorageGrid.Any())
            return;

        var boundingGrid = SharedStorageSystem.GetBoundingBox(storageComp.StorageGrid);
        var size = _emptyTexture!.Size * 2;

        //todo. at some point, we may want to only rebuild the pieces that have actually received new data.

        _pieceGrid.Children.Clear();
        _pieceGrid.Rows = boundingGrid.Height + 1;
        _pieceGrid.Rows = boundingGrid.Height + 1;
        _pieceGrid.Columns = boundingGrid.Width + 1;
        for (var y = boundingGrid.Bottom; y <= boundingGrid.Top; y++)
        {
            for (var x = boundingGrid.Left; x <= boundingGrid.Right; x++)
            {
                var currentPosition = new Vector2i(x, y);
                var item = storageComp.StoredItems
                    .Where(pair => pair.Value.Position == currentPosition)
                    .FirstOrNull();

                var control = new Control
                {
                    MinSize = size
                };

                if (item != null)
                {
                    var itemEnt = _entity.GetEntity(item.Value.Key);

                    if (_entity.TryGetComponent<ItemComponent>(itemEnt, out var itemEntComponent))
                    {
                        var gridPiece = new ItemGridPiece((itemEnt, itemEntComponent), item.Value.Value, _entity)
                        {
                            MinSize = size,
                        };
                        gridPiece.OnPiecePressed += OnPiecePressed;
                        gridPiece.OnPieceUnpressed += OnPieceUnpressed;

                        control.AddChild(gridPiece);
                    }
                }

                _pieceGrid.AddChild(control);
            }
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!IsOpen)
            return;

        _itemSystem ??= _entity.System<ItemSystem>();
        _storageSystem ??= _entity.System<StorageSystem>();
        _handsSystem ??= _entity.System<HandsSystem>();

        foreach (var child in _backgroundGrid.Children)
        {
            child.ModulateSelfOverride = Color.FromHex("#222222");
        }

        if (UserInterfaceManager.CurrentlyHovered is StorageContainer con && con != this)
            return;

        if (!_entity.TryGetComponent<StorageComponent>(StorageEntity, out var storageComponent))
            return;

        EntityUid currentEnt;
        ItemStorageLocation currentLocation;
        var usingInHand = false;
        if (_storageController.IsDragging && _storageController.DraggingGhost is { } dragging)
        {
            currentEnt = dragging.Entity;
            currentLocation = dragging.Location;
        }
        else if (_handsSystem.GetActiveHandEntity() is { } handEntity &&
                 _storageSystem.CanInsert(StorageEntity.Value, handEntity, out _, storageComp: storageComponent, ignoreLocation: true))
        {
            currentEnt = handEntity;
            currentLocation = new ItemStorageLocation(_storageController.DraggingRotation, Vector2i.Zero);
            usingInHand = true;
        }
        else
        {
            return;
        }

        if (!_entity.TryGetComponent<ItemComponent>(currentEnt, out var itemComp))
            return;

        var origin = GetMouseGridPieceLocation((currentEnt, itemComp), currentLocation);

        var itemShape = _itemSystem.GetAdjustedItemShape(
            (currentEnt, itemComp),
            currentLocation.Rotation,
            origin);
        var itemBounding = SharedStorageSystem.GetBoundingBox(itemShape);

        var validLocation = _storageSystem.ItemFitsInGridLocation(
            (currentEnt, itemComp),
            (StorageEntity.Value, storageComponent),
            origin,
            currentLocation.Rotation);

        var validColor = usingInHand ? Color.Goldenrod : Color.Green;

        for (var y = itemBounding.Bottom; y <= itemBounding.Top; y++)
        {
            for (var x = itemBounding.Left; x <= itemBounding.Right; x++)
            {
                if (TryGetBackgroundCell(x, y, out var cell) && itemShape.Any(b => b.Contains(x, y)))
                {
                    cell.ModulateSelfOverride = validLocation ? validColor : Color.Red;
                }
            }
        }
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        if (_storageController.StaticStorageUIEnabled)
            return DragMode.None;

        if (_sidebar.SizeBox.Contains(relativeMousePos - _sidebar.Position))
        {
            return DragMode.Move;
        }

        return DragMode.None;
    }

    public Vector2i GetMouseGridPieceLocation(Entity<ItemComponent?> entity, ItemStorageLocation location)
    {
        _itemSystem ??= _entity.System<ItemSystem>();
        var origin = Vector2i.Zero;

        if (StorageEntity != null)
            origin = SharedStorageSystem.GetBoundingBox(_entity.GetComponent<StorageComponent>(StorageEntity.Value).StorageGrid).BottomLeft;

        var textureSize = (Vector2) _emptyTexture!.Size * 2;
        var position = ((UserInterfaceManager.MousePositionScaled.Position
                         - _backgroundGrid.GlobalPosition
                         - ItemGridPiece.GetCenterOffset(entity, location, _entity) * 2
                         + textureSize / 2f)
                        / textureSize).Floored() + origin;
        return position;
    }

    public bool TryGetBackgroundCell(int x, int y, [NotNullWhen(true)] out Control? cell)
    {
        cell = null;

        if (!_entity.TryGetComponent<StorageComponent>(StorageEntity, out var storageComponent))
            return false;
        var boundingBox = SharedStorageSystem.GetBoundingBox(storageComponent.StorageGrid);
        x -= boundingBox.Left;
        y -= boundingBox.Bottom;

        if (x < 0 ||
            x >= _backgroundGrid.Columns ||
            y < 0 ||
            y >= _backgroundGrid.Rows)
        {
            return false;
        }

        cell = _backgroundGrid.GetChild(y * _backgroundGrid.Columns + x);
        return true;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (!IsOpen)
            return;

        _itemSystem ??= _entity.System<ItemSystem>();
        _storageSystem ??= _entity.System<StorageSystem>();
        _handsSystem ??= _entity.System<HandsSystem>();

        if (args.Function == ContentKeyFunctions.MoveStoredItem && StorageEntity != null)
        {
            //todo de-dup this idk
            if (_handsSystem.GetActiveHandEntity() is { } handEntity &&
                _storageSystem.CanInsert(StorageEntity.Value, handEntity, out _))
            {
                var pos = GetMouseGridPieceLocation((handEntity, null),
                    new ItemStorageLocation(_storageController.DraggingRotation, Vector2i.Zero));

                _entity.RaisePredictiveEvent(new StorageInsertItemIntoLocationEvent(
                    _entity.GetNetEntity(handEntity),
                    _entity.GetNetEntity(StorageEntity.Value),
                    new ItemStorageLocation(_storageController.DraggingRotation, pos)));
                args.Handle();
            }
        }
    }

    public override void Close()
    {
        base.Close();

        _storageSystem ??= _entity.System<StorageSystem>();

        if (_entity.TryGetComponent<StorageComponent>(StorageEntity, out var storageComp))
            _storageSystem?.CloseStorageUI(StorageEntity.Value, storageComp);
    }
}
