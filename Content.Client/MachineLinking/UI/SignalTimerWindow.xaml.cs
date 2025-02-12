using Content.Client.TextScreen;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Timing;

namespace Content.Client.MachineLinking.UI;

[GenerateTypedNameReferences]
public sealed partial class SignalTimerWindow : DefaultWindow
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private const int MaxTextLength = 5;

    public event Action<string>? OnCurrentTextChanged;
    public event Action<string>? OnCurrentDelayMinutesChanged;
    public event Action<string>? OnCurrentDelaySecondsChanged;

    private TimeSpan? _triggerTime;

    private bool _timerStarted;

    public event Action? OnStartTimer;

    public SignalTimerWindow()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        CurrentTextEdit.OnTextChanged += e => OnCurrentTextChange(e.Text);
        CurrentDelayEditMinutes.OnTextChanged += e => OnCurrentDelayMinutesChange(e.Text);
        CurrentDelayEditSeconds.OnTextChanged += e => OnCurrentDelaySecondsChange(e.Text);
        StartTimer.OnPressed += _ => StartTimerWeh();
    }

    private void StartTimerWeh()
    {
        if (!_timerStarted)
        {
            _timerStarted = true;
            _triggerTime = _timing.CurTime + GetDelay();
        }
        else
        {
            SetTimerStarted(false);
        }

        OnStartTimer?.Invoke();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_timerStarted || _triggerTime == null)
            return;

        if (_timing.CurTime < _triggerTime.Value)
        {
            StartTimer.Text = TextScreenSystem.TimeToString(_triggerTime.Value - _timing.CurTime);
        }
        else
        {
            SetTimerStarted(false);
        }
    }

    public void OnCurrentTextChange(string text)
    {
        if (CurrentTextEdit.Text.Length > MaxTextLength)
        {
            CurrentTextEdit.Text = CurrentTextEdit.Text.Remove(MaxTextLength);
            CurrentTextEdit.CursorPosition = MaxTextLength;
        }
        OnCurrentTextChanged?.Invoke(text);
    }

    public void OnCurrentDelayMinutesChange(string text)
    {
        List<char> toRemove = new();

        foreach (var a in text)
        {
            if (!char.IsDigit(a))
                toRemove.Add(a);
        }

        foreach (var a in toRemove)
        {
            CurrentDelayEditMinutes.Text = text.Replace(a.ToString(),"");
        }

        if (CurrentDelayEditMinutes.Text == "")
            return;

        while (CurrentDelayEditMinutes.Text[0] == '0' && CurrentDelayEditMinutes.Text.Length > 2)
        {
            CurrentDelayEditMinutes.Text = CurrentDelayEditMinutes.Text.Remove(0, 1);
        }

        if (CurrentDelayEditMinutes.Text.Length > 2)
        {
            CurrentDelayEditMinutes.Text = CurrentDelayEditMinutes.Text.Remove(2);
        }
        OnCurrentDelayMinutesChanged?.Invoke(CurrentDelayEditMinutes.Text);
    }

    public void OnCurrentDelaySecondsChange(string text)
    {
        List<char> toRemove = new();

        foreach (var a in text)
        {
            if (!char.IsDigit(a))
                toRemove.Add(a);
        }

        foreach (var a in toRemove)
        {
            CurrentDelayEditSeconds.Text = text.Replace(a.ToString(), "");
        }

        if (CurrentDelayEditSeconds.Text == "")
            return;

        while (CurrentDelayEditSeconds.Text[0] == '0' && CurrentDelayEditSeconds.Text.Length > 2)
        {
            CurrentDelayEditSeconds.Text = CurrentDelayEditSeconds.Text.Remove(0, 1);
        }

        if (CurrentDelayEditSeconds.Text.Length > 2)
        {
            CurrentDelayEditSeconds.Text = CurrentDelayEditSeconds.Text.Remove(2);
        }
        OnCurrentDelaySecondsChanged?.Invoke(CurrentDelayEditSeconds.Text);
    }

    public void SetCurrentText(string text)
    {
        CurrentTextEdit.Text = text;
    }

    public void SetCurrentDelayMinutes(string delay)
    {
        CurrentDelayEditMinutes.Text = delay;
    }

    public void SetCurrentDelaySeconds(string delay)
    {
        CurrentDelayEditSeconds.Text = delay;
    }

    public void SetShowText(bool showTime)
    {
        TextEdit.Visible = showTime;
    }

    public void SetTriggerTime(TimeSpan timeSpan)
    {
        _triggerTime = timeSpan;
    }

    public void SetTimerStarted(bool timerStarted)
    {
        _timerStarted = timerStarted;

        if (!timerStarted)
            StartTimer.Text = Loc.GetString("signal-timer-menu-start");
    }

    /// <summary>
    ///     Disables fields and buttons if you don't have the access.
    /// </summary>
    public void SetHasAccess(bool hasAccess)
    {
        CurrentTextEdit.Editable = hasAccess;
        CurrentDelayEditMinutes.Editable = hasAccess;
        CurrentDelayEditSeconds.Editable = hasAccess;
        StartTimer.Disabled = !hasAccess;
    }

    /// <summary>
    ///     Returns a TimeSpan from the currently entered delay.
    /// </summary>
    public TimeSpan GetDelay()
    {
        if (!double.TryParse(CurrentDelayEditMinutes.Text, out var minutes))
            minutes = 0;
        if (!double.TryParse(CurrentDelayEditSeconds.Text, out var seconds))
            seconds = 0;
        return TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
    }
}
