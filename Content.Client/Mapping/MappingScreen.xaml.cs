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
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.Mapping;

[GenerateTypedNameReferences]
public sealed partial class MappingScreen : InGameScreen
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public DecalPlacementSystem DecalSystem = default!;

    private PaletteColorPicker? _picker;

    private ProtoId<DecalPrototype>? _id;
    private readonly FloatSpinBox _rotationSpinBox;
    public Color DecalColor { get; private set; } = Color.White;
    private bool _decalEnableColor;
    private float _decalRotation;
    private bool _decalSnap;
    private int _decalZIndex;
    private bool _decalCleanable;

    private bool _decalAuto;

    public override ChatBox ChatBox => GetWidget<ChatBox>()!;

    public MappingScreen()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        AutoscaleMaxResolution = new Vector2i(1080, 770);

        SetAnchorPreset(LeftContainer, LayoutPreset.Wide);
        SetAnchorPreset(ViewportContainer, LayoutPreset.Wide);
        SetAnchorPreset(SpawnContainer, LayoutPreset.Wide);
        SetAnchorPreset(MainViewport, LayoutPreset.Wide);
        SetAnchorAndMarginPreset(Hotbar, LayoutPreset.BottomWide, margin: 5);
        SetAnchorAndMarginPreset(Actions, LayoutPreset.TopWide, margin: 5);

        LeftContainer.OnSplitResizeFinished += () =>
            OnChatResized?.Invoke(new Vector2(LeftContainer.SplitFraction, 0));

        _rotationSpinBox = new FloatSpinBox(90.0f, 0)
        {
            HorizontalExpand = true
        };
        DecalSpinBoxContainer.AddChild(_rotationSpinBox);

        DecalColorPicker.OnColorChanged += OnDecalColorPicked;
        DecalPickerOpen.OnPressed += OnDecalPickerOpenPressed;
        _rotationSpinBox.OnValueChanged += args =>
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
        DecalEnableColor.OnToggled += args =>
        {
            _decalEnableColor = args.Pressed;
            UpdateDecal();
            RefreshDecalList();
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
        HideLeftSide.Texture.TexturePath = "/Textures/Interface/VerbIcons/caret-left-solid.svg.192dpi.png";
        HideRightSide.Texture.TexturePath = "/Textures/Interface/VerbIcons/caret-right-solid.svg.192dpi.png";

        Flip.OnPressed += _ => FlipSides();
        HideLeftSide.OnPressed += OnToggleLeftContainer;
        HideRightSide.OnPressed += OnToggleRightContainer;

        var eraseGroup = new ButtonGroup();
        EraseDecalButton.Group = eraseGroup;
        EraseTileButton.Group = eraseGroup;
        EraseEntityButton.Group = eraseGroup;
    }

    private void FlipSides()
    {
        LeftContainer.Flip();
        RightContainer.Flip();

        if (SpawnContainer.GetPositionInParent() == 0)
        {
            Flip.Texture.TexturePath = "/Textures/Interface/VerbIcons/rotate_cw.svg.192dpi.png";

            HideLeftSide.OnPressed -= OnToggleRightContainer;
            HideLeftSide.OnPressed += OnToggleLeftContainer;

            HideRightSide.OnPressed -= OnToggleLeftContainer;
            HideRightSide.OnPressed += OnToggleRightContainer;

            SetToggleButtonTexture(HideLeftSide, SpawnContainer);
            SetToggleButtonTexture(HideRightSide, RightSpawnContainer);
        }
        else
        {
            Flip.Texture.TexturePath = "/Textures/Interface/VerbIcons/rotate_ccw.svg.192dpi.png";

            HideLeftSide.OnPressed -= OnToggleLeftContainer;
            HideLeftSide.OnPressed += OnToggleRightContainer;

            HideRightSide.OnPressed -= OnToggleRightContainer;
            HideRightSide.OnPressed += OnToggleLeftContainer;

            SetToggleButtonTexture(HideLeftSide, RightSpawnContainer);
            SetToggleButtonTexture(HideRightSide, SpawnContainer);
        }
    }

    private void OnToggleLeftContainer(ButtonEventArgs args)
    {
        SpawnContainer.Visible = !SpawnContainer.Visible;

        if (args.Button is MappingActionsButton button)
            SetToggleButtonTexture(button, SpawnContainer);
    }

    private void OnToggleRightContainer(ButtonEventArgs args)
    {
        RightSpawnContainer.Visible = !RightSpawnContainer.Visible;

        if (args.Button is MappingActionsButton button)
            SetToggleButtonTexture(button, RightSpawnContainer);
    }

    private static void SetToggleButtonTexture(MappingActionsButton button, BoxContainer container)
    {
        if (container.GetPositionInParent() == 0)
        {
            button.Texture.TexturePath = container.Visible
                ? "/Textures/Interface/VerbIcons/caret-left-solid.svg.192dpi.png"
                : "/Textures/Interface/VerbIcons/caret-right-solid.svg.192dpi.png";
        }
        else
        {
            button.Texture.TexturePath = container.Visible
                ? "/Textures/Interface/VerbIcons/caret-right-solid.svg.192dpi.png"
                : "/Textures/Interface/VerbIcons/caret-left-solid.svg.192dpi.png";
        }
    }

    private void OnDecalColorPicked(Color color)
    {
        DecalColor = color;
        DecalColorPicker.Color = color;
        UpdateDecal();
        RefreshDecalList();
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

        DecalSystem.UpdateDecalInfo(id, _decalEnableColor ? DecalColor : Color.White, _decalRotation, _decalSnap, _decalZIndex, _decalCleanable);
    }

    public void SelectDecal(string decalId)
    {
        if (!_prototype.TryIndex<DecalPrototype>(decalId, out var decal))
            return;

        _id = decalId;

        if (_decalAuto)
        {
            _decalEnableColor = decal.DefaultCustomColor;
            _decalCleanable = decal.DefaultCleanable;
            _decalSnap = decal.DefaultSnap;

            DecalColorPicker.Color = DecalColor;
            DecalEnableCleanable.Pressed = _decalCleanable;
            DecalEnableSnap.Pressed = _decalSnap;
            DecalEnableColor.Pressed = _decalEnableColor;
        }

        UpdateDecal();
        RefreshDecalList();
    }

    public void SelectDecal(Decal decal)
    {
        if (!_decalAuto)
            return;

        _id = decal.Id;
        _decalCleanable = decal.Cleanable;

        if (decal.Color is { } color)
            DecalColor = color;
        else
            _decalEnableColor = false;

        DecalColorPicker.Color = DecalColor;
        DecalEnableCleanable.Pressed = _decalCleanable;
        DecalEnableSnap.Pressed = _decalSnap;
        DecalEnableColor.Pressed = _decalEnableColor;

        UpdateDecal();
        RefreshDecalList();
    }

    public void ChangeDecalRotation(float rotation)
    {
        _decalRotation += rotation;

        if (_decalRotation > 360)
            _decalRotation = 0;
        if (_decalRotation < 0)
            _decalRotation = 360;

        _rotationSpinBox.Value = _decalRotation;
        UpdateDecal();
    }

    private void RefreshDecalList()
    {
        Decals.TexturesModulate = _decalEnableColor ? DecalColor : Color.White;
        var children = Decals.PrototypeList.Children.ToList().Union(Decals.SearchList.Children);
        foreach (var control in children)
        {
            if (control is not MappingSpawnButton button)
                continue;

            RefreshDecalButton(button);
        }
    }

    private void RefreshDecalButton(MappingSpawnButton button)
    {
        var children =
            button.ChildrenPrototypes.Children.ToList().Union(button.ChildrenPrototypesGallery.Children);

        foreach (var control in children)
        {
            if (control is not MappingSpawnButton { } childButton)
                continue;

            if (childButton.Texture.Visible)
                childButton.Texture.Modulate = _decalEnableColor ? DecalColor : Color.White;

            RefreshDecalButton(childButton);
        }
    }

    public override void SetChatSize(Vector2 size)
    {
        LeftContainer.ResizeMode = SplitContainer.SplitResizeMode.RespectChildrenMinSize;
    }

    public void UnPressActionsExcept(Control except)
    {
        Add.Pressed = Add == except;
        Fill.Pressed = Fill == except;
        Grab.Pressed = Grab == except;
        Move.Pressed = Move == except;
        Pick.Pressed = Pick == except;

        EraseEntityButton.Pressed = EraseEntityButton == except;
        EraseDecalButton.Pressed = EraseDecalButton == except;
        EraseTileButton.Pressed = EraseTileButton == except;
    }
}
