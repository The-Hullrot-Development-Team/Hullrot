﻿using Content.Shared.Actions.ActionTypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Utility;
using Robust.Shared.Input;

namespace Content.Client.UserInterface.Systems.Actions.Controls;

public sealed class ActionButton : Control
{
    public BoundKeyFunction? KeyBind
    {
        set
        {
            _keybind = value;
            if (_keybind != null)
            {
                Label.Text = BoundKeyHelper.ShortKeyName(_keybind.Value);
            }
        }
    }

    private BoundKeyFunction? _keybind;
    public readonly TextureRect Button;
    public readonly TextureRect Icon;
    public readonly Label Label;
    public readonly SpriteView Sprite;
    public Texture? IconTexture
    {
        get => Icon.Texture;
        private set => Icon.Texture = value;
    }

    public ActionType? Action { get; private set; }
    public bool Locked { get; set; }

    public event Action<GUIBoundKeyEventArgs, ActionButton>? ActionPressed;
    public event Action<GUIBoundKeyEventArgs, ActionButton>? ActionUnpressed;
    public event Action<ActionButton>? ActionFocusExited;

    public ActionButton()
    {
        MouseFilter = MouseFilterMode.Pass;
        Button = new TextureRect()
        {
            Name="Button",
            TextureScale = new Vector2(2,2)
        };
        Icon = new TextureRect()
        {
            Name="Icon",
            TextureScale = new Vector2(2,2)
        };
        Label = new Label()
        {
            Name="Label",
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Top

        };
        Sprite = new SpriteView()
        {
            Name = "Sprite",
            OverrideDirection = Direction.South
        };

        AddChild(Button);
        AddChild(Icon);
        AddChild(Label);
        AddChild(Sprite);

        Button.Texture = Theme.ResolveTexture("SlotBackground");
        Button.Modulate = new Color(255, 255, 255, 150);

        Icon.Modulate = new Color(255, 255, 255, 150);

        Label.FontColorOverride = Theme.ResolveColorOrSpecified("whiteText");

        OnKeyBindDown += OnPressed;
        OnKeyBindUp += OnUnpressed;
    }

    private void OnPressed(GUIBoundKeyEventArgs args)
    {
        ActionPressed?.Invoke(args, this);
    }

    private void OnUnpressed(GUIBoundKeyEventArgs args)
    {
        ActionUnpressed?.Invoke(args, this);
    }

    protected override void ControlFocusExited()
    {
        ActionFocusExited?.Invoke(this);
    }

    public void TryReplaceWith(IEntityManager entityManager, ActionType action)
    {
        if (!Locked)
            UpdateData(entityManager, action);
    }

    public void UpdateData(IEntityManager entityManager, ActionType action)
    {
        Action = action;

        if (action.Provider == null || !entityManager.TryGetComponent(action.Provider.Value, out SpriteComponent sprite))
        {
            if (action.Icon != null)
            {
                IconTexture = action.Icon.Frame0();
            }
            Sprite.Sprite = null;
        }
        else
        {
            Sprite.Sprite = sprite;
        }
    }

    public void ClearData()
    {
        Action = null;
        IconTexture = null;
        Sprite.Sprite = null;
    }
}
