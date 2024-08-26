using System.Text.RegularExpressions;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Shared.Chat;
using Content.Shared.Input;
using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Configuration;
using Robust.Shared.Audio;
using Robust.Shared.Input;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.LineEdit;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

[GenerateTypedNameReferences]
#pragma warning disable RA0003
public partial class ChatBox : UIWidget
#pragma warning restore RA0003
{
    private static readonly Color HighlightColor = Color.FromHex("#e5ffcc");
    private readonly ChatUIController _controller;
    private readonly IEntityManager _entManager;

    private List<string> _keywords = [];

    public bool Main { get; set; }

    public ChatSelectChannel SelectedChannel => ChatInput.ChannelSelector.SelectedChannel;

    public ChatBox()
    {
        RobustXamlLoader.Load(this);
        _entManager = IoCManager.Resolve<IEntityManager>();

        ChatInput.Input.OnTextEntered += OnTextEntered;
        ChatInput.Input.OnKeyBindDown += OnInputKeyBindDown;
        ChatInput.Input.OnTextChanged += OnTextChanged;
        ChatInput.ChannelSelector.OnChannelSelect += OnChannelSelect;
        ChatInput.FilterButton.Popup.OnChannelFilter += OnChannelFilter;
        ChatInput.FilterButton.Popup.OnNewHighlight += SetHighlights;

        _controller = UserInterfaceManager.GetUIController<ChatUIController>();
        _controller.MessageAdded += OnMessageAdded;
        _controller.RegisterChat(this);
    }

    private void OnTextEntered(LineEditEventArgs args)
    {
        _controller.SendMessage(this, SelectedChannel);
    }

    private void OnMessageAdded(ChatMessage msg)
    {
        Logger.DebugS("chat", $"{msg.Channel}: {msg.Message}");
        if (!ChatInput.FilterButton.Popup.IsActive(msg.Channel))
        {
            return;
        }

        if (msg is { Read: false, AudioPath: { } })
            _entManager.System<AudioSystem>().PlayGlobal(msg.AudioPath, Filter.Local(), false, AudioParams.Default.WithVolume(msg.AudioVolume));

        msg.Read = true;

        var color = msg.MessageColorOverride ?? msg.Channel.TextColor();
        
        // Highlight any words choosen by the client.
        foreach (var keyword in _keywords)
        {
            msg.WrappedMessage = SharedChatSystem.InjectTagAroundString(msg, keyword, "color", HighlightColor.ToHex());
        }

        AddLine(msg.WrappedMessage, color);
    }

    private void OnChannelSelect(ChatSelectChannel channel)
    {
        _controller.UpdateSelectedChannel(this);
    }

    public void Repopulate()
    {
        Contents.Clear();

        foreach (var message in _controller.History)
        {
            OnMessageAdded(message.Item2);
        }
    }

    private void OnChannelFilter(ChatChannel channel, bool active)
    {
        Contents.Clear();

        foreach (var message in _controller.History)
        {
            OnMessageAdded(message.Item2);
        }

        if (active)
        {
            _controller.ClearUnfilteredUnreads(channel);
        }
    }

    public void AddLine(string message, Color color)
    {
        var formatted = new FormattedMessage(3);
        formatted.PushColor(color);
        formatted.AddMarkup(message);
        formatted.Pop();
        Contents.AddMessage(formatted);
    }

    public void Focus(ChatSelectChannel? channel = null)
    {
        var input = ChatInput.Input;
        var selectStart = Index.End;

        if (channel != null)
            ChatInput.ChannelSelector.Select(channel.Value);

        input.IgnoreNext = true;
        input.GrabKeyboardFocus();

        input.CursorPosition = input.Text.Length;
        input.SelectionStart = selectStart.GetOffset(input.Text.Length);
    }

    public void CycleChatChannel(bool forward)
    {
        var idx = Array.IndexOf(ChannelSelectorPopup.ChannelSelectorOrder, SelectedChannel);
        do
        {
            // go over every channel until we find one we can actually select.
            idx += forward ? 1 : -1;
            idx = MathHelper.Mod(idx, ChannelSelectorPopup.ChannelSelectorOrder.Length);
        } while ((_controller.SelectableChannels & ChannelSelectorPopup.ChannelSelectorOrder[idx]) == 0);

        SafelySelectChannel(ChannelSelectorPopup.ChannelSelectorOrder[idx]);
    }

    public void SafelySelectChannel(ChatSelectChannel toSelect)
    {
        toSelect = _controller.MapLocalIfGhost(toSelect);
        if ((_controller.SelectableChannels & toSelect) == 0)
            return;

        ChatInput.ChannelSelector.Select(toSelect);
    }

    private void OnInputKeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.TextReleaseFocus)
        {
            ChatInput.Input.ReleaseKeyboardFocus();
            ChatInput.Input.Clear();
            args.Handle();
            return;
        }

        if (args.Function == ContentKeyFunctions.CycleChatChannelForward)
        {
            CycleChatChannel(true);
            args.Handle();
            return;
        }

        if (args.Function == ContentKeyFunctions.CycleChatChannelBackward)
        {
            CycleChatChannel(false);
            args.Handle();
        }
    }

    private void OnTextChanged(LineEditEventArgs args)
    {
        // Update channel select button to correct channel if we have a prefix.
        _controller.UpdateSelectedChannel(this);

        // Warn typing indicator about change
        _controller.NotifyChatTextChange();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing) return;
        _controller.UnregisterChat(this);
        ChatInput.Input.OnTextEntered -= OnTextEntered;
        ChatInput.Input.OnKeyBindDown -= OnInputKeyBindDown;
        ChatInput.Input.OnTextChanged -= OnTextChanged;
        ChatInput.ChannelSelector.OnChannelSelect -= OnChannelSelect;
    }
    
    private void SetHighlights(string highlights)
    {
        // Save the newly provided list of highlighs if different.
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        if (!cfg.GetCVar(CCVars.ChatHighlights).Equals(highlights, StringComparison.CurrentCultureIgnoreCase))
        {
            cfg.SetCVar(CCVars.ChatHighlights, highlights);
            cfg.SaveToFile();
        }

        // Fill the array with keywords separated by commas, disregarding empty entries.
        string[] arr_keywords = highlights.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _keywords.Clear();
        foreach (var keyword in arr_keywords)
        {
            _keywords.Add(keyword);
        }
    }
}
