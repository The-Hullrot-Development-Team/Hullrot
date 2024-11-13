using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Speech;
using Content.Shared.Whitelist;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Chat.UI;

[GenerateTypedNameReferences]
public sealed partial class EmotesMenu : RadialMenu
{
    [Dependency] private readonly EntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public event Action<ProtoId<EmotePrototype>, string?>? OnPlayEmote;

    public EmotesMenu()
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);

        var spriteSystem = _entManager.System<SpriteSystem>();
        var whitelistSystem = _entManager.System<EntityWhitelistSystem>();

        var main = FindControl<RadialContainer>("Main");

        var emotes = _prototypeManager.EnumeratePrototypes<EmotePrototype>();
        foreach (var emote in emotes)
        {
            var player = _playerManager.LocalSession?.AttachedEntity;
            if (emote.Category == EmoteCategory.Invalid ||
                emote.ChatTriggers.Count == 0 && !emote.IsCustomContentType ||
                !(player.HasValue && whitelistSystem.IsWhitelistPassOrNull(emote.Whitelist, player.Value)) ||
                whitelistSystem.IsBlacklistPass(emote.Blacklist, player.Value))
                continue;

            if (!emote.Available &&
                _entManager.TryGetComponent<SpeechComponent>(player.Value, out var speech) &&
                !speech.AllowedEmotes.Contains(emote.ID))
                continue;

            string name;

            if (emote.IsCustomContentType)
            {
                var value = _cfg.GetCVar<string>(emote.CustomContentCVar!).Trim();

                if (string.IsNullOrEmpty(value))
                    continue;

                name = value;
            }
            else
            {
                name = Loc.GetString(emote.Name);
            }

            var parent = FindControl<RadialContainer>(emote.Category.ToString());

            var button = new EmoteMenuButton
            {
                StyleClasses = { "RadialMenuButton" },
                SetSize = new Vector2(64f, 64f),
                ToolTip = name,
                ProtoId = emote.ID,
            };

            if (emote.IsCustomContentType)
                button.CustomEmoteContent = name;

            var tex = new TextureRect
            {
                VerticalAlignment = VAlignment.Center,
                HorizontalAlignment = HAlignment.Center,
                Texture = spriteSystem.Frame0(emote.Icon),
                TextureScale = new Vector2(2f, 2f),
            };

            button.AddChild(tex);
            parent.AddChild(button);
            foreach (var child in main.Children)
            {
                if (child is not RadialMenuTextureButton castChild)
                    continue;

                if (castChild.TargetLayer == emote.Category.ToString())
                {
                    castChild.Visible = true;
                    break;
                }
            }
        }

        // Set up menu actions
        foreach (var child in Children)
        {
            if (child is not RadialContainer container)
                continue;
            AddEmoteClickAction(container);
        }
    }

    private void AddEmoteClickAction(RadialContainer container)
    {
        foreach (var child in container.Children)
        {
            if (child is not EmoteMenuButton castChild)
                continue;

            castChild.OnButtonUp += _ =>
            {
                OnPlayEmote?.Invoke(castChild.ProtoId, castChild.CustomEmoteContent);
                Close();
            };
        }
    }
}


public sealed class EmoteMenuButton : RadialMenuTextureButton
{
    public ProtoId<EmotePrototype> ProtoId { get; set; }
    public string? CustomEmoteContent { get; set; }
}
