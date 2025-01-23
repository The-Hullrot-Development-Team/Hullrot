using System.Linq;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Input;
using Content.Shared.Speech;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Emotes;

[UsedImplicitly]
public sealed class EmotesUIController : UIController, IOnStateChanged<GameplayState>
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IClyde _displayManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    
    private MenuButton? EmotesButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.EmotesButton;
    private RadialMenu? _menu;

    private static readonly Dictionary<EmoteCategory, (string Tooltip, SpriteSpecifier Sprite)> EmoteGroupingInfo
        = new Dictionary<EmoteCategory, (string Tooltip, SpriteSpecifier Sprite)>
    {
        [EmoteCategory.General] = ("emote-menu-category-general", new SpriteSpecifier.Texture(new ResPath("/Textures/Clothing/Head/Soft/mimesoft.rsi/icon.png"))),
        [EmoteCategory.Hands] = ("emote-menu-category-hands", new SpriteSpecifier.Texture(new ResPath("/Textures/Clothing/Hands/Gloves/latex.rsi/icon.png"))),
        [EmoteCategory.Vocal] = ("emote-menu-category-vocal", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Emotes/vocal.png"))),
    };

    public void OnStateEntered(GameplayState state)
    {
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenEmotesMenu,
                InputCmdHandler.FromDelegate(_ => ToggleEmotesMenu(false)))
            .Register<EmotesUIController>();
    }

    public void OnStateExited(GameplayState state)
    {
        CommandBinds.Unregister<EmotesUIController>();
    }

    private void ToggleEmotesMenu(bool centered)
    {
        if (_menu == null)
        {
            // setup window
            var prototypes = _prototypeManager.EnumeratePrototypes<EmotePrototype>();
            var models = ConvertToButtons(prototypes);


            _menu = new SimpleRadialMenu(models);
            _menu.Open();
            _menu.OnClose += OnWindowClosed;
            _menu.OnOpen += OnWindowOpen;

            if (EmotesButton != null)
                EmotesButton.SetClickPressed(true);

            if (centered)
            {
                _menu.OpenCentered();
            }
            else
            {
                // Open the menu, centered on the mouse
                var vpSize = _displayManager.ScreenSize;
                _menu.OpenCenteredAt(_inputManager.MouseScreenPosition.Position / vpSize);
            }
        }
        else
        {
            _menu.OnClose -= OnWindowClosed;
            _menu.OnOpen -= OnWindowOpen;

            if (EmotesButton != null)
                EmotesButton.SetClickPressed(false);

            CloseMenu();
        }
    }

    public void UnloadButton()
    {
        if (EmotesButton == null)
            return;

        EmotesButton.OnPressed -= ActionButtonPressed;
    }

    public void LoadButton()
    {
        if (EmotesButton == null)
            return;

        EmotesButton.OnPressed += ActionButtonPressed;
    }

    private void ActionButtonPressed(BaseButton.ButtonEventArgs args)
    {
        ToggleEmotesMenu(true);
    }

    private void OnWindowClosed()
    {
        if (EmotesButton != null)
            EmotesButton.Pressed = false;

        CloseMenu();
    }

    private void OnWindowOpen()
    {
        if (EmotesButton != null)
            EmotesButton.Pressed = true;
    }

    private void CloseMenu()
    {
        if (_menu == null)
            return;

        _menu.Dispose();
        _menu = null;
    }

    private IEnumerable<RadialMenuOption> ConvertToButtons(IEnumerable<EmotePrototype> emotePrototypes)
    {
        var whitelistSystem = EntitySystemManager.GetEntitySystem<EntityWhitelistSystem>();
        var player = _playerManager.LocalSession?.AttachedEntity;
        var models = emotePrototypes.GroupBy(x => x.Category)
                                    .Where(x => x.Key != EmoteCategory.Invalid)
                                    .Select(categoryGroup =>
                                    {
                                        var nestedEmotes = categoryGroup.Where(emote =>
                                        {
                                            // only valid emotes that have ways to be triggered by chat and player have access / no restriction on
                                            if (emote.Category == EmoteCategory.Invalid
                                                || emote.ChatTriggers.Count == 0
                                                || !(player.HasValue && whitelistSystem.IsWhitelistPassOrNull(emote.Whitelist, player.Value))
                                                || whitelistSystem.IsBlacklistPass(emote.Blacklist, player.Value))
                                                return false;

                                            // emotes that are available by default, and requires speech and player can speak, or it doesn't require speech
                                            return emote.Available
                                                   || !EntityManager.TryGetComponent<SpeechComponent>(player.Value, out var speech)
                                                   || speech.AllowedEmotes.Contains(emote.ID);
                                        }).Select(
                                            emote => new RadialMenuActionOption(() => _entityManager.RaisePredictiveEvent(new PlayEmoteMessage(emote.ID)))
                                            {
                                                Sprite = emote.Icon,
                                                ToolTip = Loc.GetString(emote.Name)
                                            }
                                        ).ToArray();
                                        var tuple = EmoteGroupingInfo[categoryGroup.Key];
                                        return new RadialMenuNestedLayerOption(nestedEmotes)
                                        {
                                            Sprite = tuple.Sprite,
                                            ToolTip = Loc.GetString(tuple.Tooltip)
                                        };
                                    });
        return models;
    }
}
