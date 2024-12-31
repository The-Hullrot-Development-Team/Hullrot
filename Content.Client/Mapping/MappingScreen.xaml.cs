﻿using System.Linq;
using System.Numerics;
using Content.Client.Decals;
using Content.Client.Decals.UI;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Shared.Decals;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Serilog;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.Mapping;

[GenerateTypedNameReferences]
public sealed partial class MappingScreen : InGameScreen
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public DecalPlacementSystem DecalSystem = default!;

    private PaletteColorPicker? _picker;

    private ProtoId<DecalPrototype>? _id;
    private Color _decalColor = Color.White;
    private float _decalRotation;
    private bool _decalSnap;
    private int _decalZIndex;
    private bool _decalCleanable;

    private bool _decalAuto;

    public override ChatBox ChatBox => GetWidget<ChatBox>()!;

    public event Func<MappingSpawnButton, bool>? IsDecalVisible;

    public MappingScreen()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        var layersUIController = UserInterfaceManager.GetUIController<MappingLayersUIController>();

        AutoscaleMaxResolution = new Vector2i(1080, 770);

        SetAnchorPreset(ScreenContainer, LayoutPreset.Wide);
        SetAnchorPreset(ViewportContainer, LayoutPreset.Wide);
        SetAnchorPreset(SpawnContainer, LayoutPreset.Wide);
        SetAnchorPreset(MainViewport, LayoutPreset.Wide);
        SetAnchorAndMarginPreset(Hotbar, LayoutPreset.BottomWide, margin: 5);
        SetAnchorAndMarginPreset(Actions, LayoutPreset.TopWide, margin: 5);

        ScreenContainer.OnSplitResizeFinished += () =>
            OnChatResized?.Invoke(new Vector2(ScreenContainer.SplitFraction, 0));

        var rotationSpinBox = new FloatSpinBox(90.0f, 0)
        {
            HorizontalExpand = true
        };
        DecalSpinBoxContainer.AddChild(rotationSpinBox);

        DecalColorPicker.OnColorChanged += OnDecalColorPicked;
        DecalPickerOpen.OnPressed += OnDecalPickerOpenPressed;
        rotationSpinBox.OnValueChanged += args =>
        {
            _decalRotation = args.Value;
            UpdateDecal();
        };
        DecalEnableAuto.OnToggled += args =>
        {
            _decalAuto = args.Pressed;
            if (_id is { } id)
                SelectDecal(id);
        };
        DecalEnableSnap.OnToggled += args =>
        {
            _decalSnap = args.Pressed;
            UpdateDecal();
        };
        DecalEnableCleanable.OnToggled += args =>
        {
            _decalCleanable = args.Pressed;
            UpdateDecal();
        };
        DecalZIndexSpinBox.ValueChanged += args =>
        {
            _decalZIndex = args.Value;
            UpdateDecal();
        };

        for (var i = 0; i < EntitySpawnWindow.InitOpts.Length; i++)
        {
            EntityPlacementMode.AddItem(EntitySpawnWindow.InitOpts[i], i);
        }

        Pick.Texture.TexturePath = "/Textures/Interface/eyedropper.svg.png";
        Flip.Texture.TexturePath = "/Textures/Interface/VerbIcons/rotate_cw.svg.192dpi.png";
        Flip.OnPressed += args => FlipSides();
        Layers.Texture.TexturePath = "/Textures/Interface/hamburger.svg.192dpi.png";
        Layers.OnPressed += args =>
        {
            layersUIController.ToggleWindow();
        };
        FixGridAtmos.Texture.TexturePath = "/Textures/Interface/VerbIcons/oxygen.svg.192dpi.png";
        RemoveGrid.Texture.TexturePath = "/Textures/Interface/VerbIcons/delete_transparent.svg.192dpi.png";
        MoveGrid.Texture.TexturePath = "/Textures/Interface/VerbIcons/point.svg.192dpi.png";
        GridVV.Texture.TexturePath = "/Textures/Interface/VerbIcons/vv.svg.192dpi.png";
    }

    public void FlipSides()
    {
        ScreenContainer.Flip();

        if (SpawnContainer.GetPositionInParent() == 0)
        {
            Flip.Texture.TexturePath = "/Textures/Interface/VerbIcons/rotate_cw.svg.192dpi.png";
        }
        else
        {
            Flip.Texture.TexturePath = "/Textures/Interface/VerbIcons/rotate_ccw.svg.192dpi.png";
        }
    }

    private void OnDecalColorPicked(Color color)
    {
        _decalColor = color;
        DecalColorPicker.Color = color;
        UpdateDecal();
    }

    private void OnDecalPickerOpenPressed(ButtonEventArgs obj)
    {
        if (_picker == null)
        {
            _picker = new PaletteColorPicker();
            _picker.OpenToLeft();
            _picker.PaletteList.OnItemSelected += args =>
            {
                var color = ((Color?) args.ItemList.GetSelected().First().Metadata)!.Value;
                OnDecalColorPicked(color);
            };

            return;
        }

        if (_picker.IsOpen)
            _picker.Close();
        else
            _picker.Open();
    }

    private void UpdateDecal()
    {
        if (_id is not { } id)
            return;

        DecalSystem.UpdateDecalInfo(id, _decalColor, _decalRotation, _decalSnap, _decalZIndex, _decalCleanable);
    }

    public void SelectDecal(string decalId)
    {
        if (!_prototype.TryIndex<DecalPrototype>(decalId, out var decal))
            return;

        _id = decalId;

        if (_decalAuto)
        {
            _decalColor = Color.White;
            _decalCleanable = decal.DefaultCleanable;
            _decalSnap = decal.DefaultSnap;

            DecalColorPicker.Color = _decalColor;
            DecalEnableCleanable.Pressed = _decalCleanable;
            DecalEnableSnap.Pressed = _decalSnap;
        }

        UpdateDecal();
        RefreshList();
    }

    private void RefreshList()
    {
        foreach (var control in Prototypes.Children)
        {
            if (control is not MappingSpawnButton button ||
                button.Prototype?.Prototype is not DecalPrototype)
            {
                continue;
            }

            foreach (var child in button.Children)
            {
                if (child is not MappingSpawnButton { Prototype.Prototype: DecalPrototype } childButton)
                {
                    continue;
                }

                childButton.Texture.Modulate = _decalColor;
                childButton.Visible = IsDecalVisible?.Invoke(childButton) ?? true;
            }
        }
    }

    public override void SetChatSize(Vector2 size)
    {
        ScreenContainer.ResizeMode = SplitContainer.SplitResizeMode.RespectChildrenMinSize;
    }

    public void UnPressActionsExcept(Control except)
    {
        Add.Pressed = Add == except;
        Fill.Pressed = Fill == except;
        Grab.Pressed = Grab == except;
        Move.Pressed = Move == except;
        Pick.Pressed = Pick == except;
        Layers.Pressed = Layers == except;
        FixGridAtmos.Pressed = FixGridAtmos == except;
        RemoveGrid.Pressed = RemoveGrid == except;
        MoveGrid.Pressed = MoveGrid == except;
        GridVV.Pressed = GridVV == except;

        EraseEntityButton.Pressed = EraseEntityButton == except;
        EraseDecalButton.Pressed = EraseDecalButton == except;
        EraseTileButton.Pressed = EraseTileButton == except;
    }
}
