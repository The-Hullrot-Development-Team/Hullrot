using Content.Client.UserInterface.Controls;
using Content.Shared.Administration.Notes;
using Content.Shared.Database;
using Robust.Client.AutoGenerated;
using Robust.Client.Console;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Administration.UI.Notes;

[GenerateTypedNameReferences]
public sealed partial class NoteEdit : FancyWindow
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IClientConsoleHost _console = default!;

    private enum Multipliers
    {
        Minutes,
        Hours,
        Days,
        Weeks,
        Months,
        Years,
        Centuries
    }

    public event Action<int, NoteType, string, NoteSeverity?, bool, DateTime?>? SubmitPressed;

    public NoteEdit(SharedAdminNote? note, string playerName, bool canCreate, bool canEdit)
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);
        PlayerName = playerName;
        Title = Loc.GetString("admin-note-editor-title-new", ("player", PlayerName));
        IsCreating = note is null;
        CanCreate = canCreate;
        CanEdit = canEdit;

        ResetSubmitButton();

        // It's weird to use minutes as the IDs, but it works and makes sense kind of :)
        ExpiryLengthDropdown.AddItem(Loc.GetString("admin-note-button-minutes"), (int) Multipliers.Minutes);
        ExpiryLengthDropdown.AddItem(Loc.GetString("admin-note-button-hours"), (int) Multipliers.Hours);
        ExpiryLengthDropdown.AddItem(Loc.GetString("admin-note-button-days"), (int) Multipliers.Days);
        ExpiryLengthDropdown.AddItem(Loc.GetString("admin-note-button-weeks"), (int) Multipliers.Weeks);
        ExpiryLengthDropdown.AddItem(Loc.GetString("admin-note-button-months"), (int) Multipliers.Months);
        ExpiryLengthDropdown.AddItem(Loc.GetString("admin-note-button-years"), (int) Multipliers.Years);
        ExpiryLengthDropdown.AddItem(Loc.GetString("admin-note-button-centuries"), (int) Multipliers.Centuries);
        ExpiryLengthDropdown.OnItemSelected += OnLengthChanged;

        ExpiryLengthDropdown.SelectId((int) Multipliers.Days);

        ExpiryLineEdit.OnTextChanged += OnTextChanged;

        TypeOption.AddItem(Loc.GetString("admin-note-editor-type-note"), (int) NoteType.Note);
        TypeOption.AddItem(Loc.GetString("admin-note-editor-type-message"), (int) NoteType.Message);
        TypeOption.AddItem(Loc.GetString("admin-note-editor-type-watchlist"), (int) NoteType.Watchlist);
        TypeOption.OnItemSelected += OnTypeChanged;


        SeverityOption.AddItem(Loc.GetString("admin-note-editor-severity-select"), -1);
        SeverityOption.AddItem(Loc.GetString("admin-note-editor-severity-none"), (int) Shared.Database.NoteSeverity.None);
        SeverityOption.AddItem(Loc.GetString("admin-note-editor-severity-low"), (int) Shared.Database.NoteSeverity.Minor);
        SeverityOption.AddItem(Loc.GetString("admin-note-editor-severity-medium"), (int) Shared.Database.NoteSeverity.Medium);
        SeverityOption.AddItem(Loc.GetString("admin-note-editor-severity-high"), (int) Shared.Database.NoteSeverity.High);
        SeverityOption.OnItemSelected += OnSeverityChanged;

        PermanentCheckBox.OnPressed += OnPermanentPressed;
        SecretCheckBox.OnPressed += OnSecretPressed;
        SubmitButton.OnPressed += OnSubmitButtonPressed;
        SubmitButton.OnMouseEntered += OnSubmitButtonMouseEntered;
        SubmitButton.OnMouseExited += OnSubmitButtonMouseExited;

        if (note is null && !canCreate)
        {
            TypeOption.Disabled = true;
            SeverityOption.Disabled = true;
        }

        if (note is not null)
        {
            Title = Loc.GetString("admin-note-editor-title-existing", ("id", note.Id), ("player", PlayerName), ("author", note.CreatedByName));
            NoteId = note.Id;

            NoteType = note.NoteType;
            TypeOption.AddItem(Loc.GetString("admin-note-editor-type-server-ban"), (int) NoteType.ServerBan);
            TypeOption.AddItem(Loc.GetString("admin-note-editor-type-role-ban"), (int) NoteType.RoleBan);
            TypeOption.SelectId((int)NoteType);
            TypeOption.Disabled = true;

            NoteTextEdit.InsertAtCursor(note.Message);

            NoteSeverity = note.NoteSeverity ?? Shared.Database.NoteSeverity.Minor;
            SeverityOption.SelectId((int)NoteSeverity);
            SeverityOption.Disabled = note.NoteType is not (NoteType.Note or NoteType.ServerBan or NoteType.RoleBan);

            IsSecret = note.Secret;
            SecretCheckBox.Pressed = note.Secret;
            SecretCheckBox.Disabled = note.NoteType is not NoteType.Note;
            ExpiryTime = note.ExpiryTime;
            if (ExpiryTime is not null)
            {
                PermanentCheckBox.Pressed = false;
                UpdatePermanentCheckboxFields();
                ExpiryLineEdit.Text = ExpiryTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        UpdateSubmitButton();
    }

    private void OnSubmitButtonMouseEntered(GUIMouseHoverEventArgs args)
    {
        if (!SubmitButton.Disabled)
            return;

        SeverityOption.ModulateSelfOverride = Color.Red;
    }

    private void OnSubmitButtonMouseExited(GUIMouseHoverEventArgs args)
    {
        SeverityOption.ModulateSelfOverride = null;
    }

    private NoteSeverity? _noteSeverity = null;

    private string PlayerName { get; }
    private int NoteId { get; }
    private bool IsSecret { get; set; }
    private NoteType NoteType { get; set; }

    private NoteSeverity? NoteSeverity
    {
        get => _noteSeverity;
        set
        {
            _noteSeverity = value;
            UpdateSubmitButton();
        }
    }
    private DateTime? ExpiryTime { get; set; }
    private TimeSpan? DeleteResetOn { get; set; }
    private bool IsCreating { get; set; }
    private bool CanCreate { get; set; }
    private bool CanEdit { get; set; }

    private void OnTypeChanged(OptionButton.ItemSelectedEventArgs args)
    {
        // We should be resetting the underlying values too but the server handles that anyway
        switch (args.Id)
        {
            case (int) NoteType.Note: // Note: your standard note, does nothing special
                NoteType = NoteType.Note;
                SecretCheckBox.Disabled = false;
                SecretCheckBox.Pressed = false;
                SeverityOption.Disabled = false;
                PermanentCheckBox.Pressed = true;
                UpdatePermanentCheckboxFields();
                break;
            case (int) NoteType.Message: // Message: these are shown to the player when they log on
                NoteType = NoteType.Message;
                SecretCheckBox.Disabled = true;
                SecretCheckBox.Pressed = false;
                SeverityOption.Disabled = true;
                SeverityOption.SelectId((int) Shared.Database.NoteSeverity.None);
                NoteSeverity = null;
                PermanentCheckBox.Pressed = false;
                UpdatePermanentCheckboxFields();
                break;
            case (int) NoteType.Watchlist: // Watchlist: these are always secret and only shown to admins when the player logs on
                NoteType = NoteType.Watchlist;
                SecretCheckBox.Disabled = true;
                SecretCheckBox.Pressed = true;
                SeverityOption.Disabled = true;
                SeverityOption.SelectId((int) Shared.Database.NoteSeverity.None);
                NoteSeverity = null;
                PermanentCheckBox.Pressed = false;
                UpdatePermanentCheckboxFields();
                break;
            default: // Wuh oh
                throw new ArgumentOutOfRangeException(nameof(args.Id), args.Id, "Unknown note type");
        }

        TypeOption.SelectId(args.Id);
    }

    private void OnPermanentPressed(BaseButton.ButtonEventArgs _)
    {
        UpdatePermanentCheckboxFields();
    }

    private void UpdatePermanentCheckboxFields()
    {
        ExpiryLabel.Visible = !PermanentCheckBox.Pressed;
        ExpiryLineEdit.Visible = !PermanentCheckBox.Pressed;
        ExpiryLengthDropdown.Visible = !PermanentCheckBox.Pressed;

        ExpiryLineEdit.Text = !PermanentCheckBox.Pressed ? 7.ToString() : string.Empty;
    }

    private void OnSecretPressed(BaseButton.ButtonEventArgs _)
    {
        IsSecret = SecretCheckBox.Pressed;
    }

    private void OnSeverityChanged(OptionButton.ItemSelectedEventArgs args)
    {
        NoteSeverity = args.Id == -1 ? NoteSeverity = null : (NoteSeverity) args.Id;
        SeverityOption.SelectId(args.Id);
    }

    private void OnLengthChanged(OptionButton.ItemSelectedEventArgs args)
    {
        ExpiryLengthDropdown.SelectId(args.Id);
    }

    private void OnTextChanged(HistoryLineEdit.LineEditEventArgs args)
    {
        ParseExpiryTime();
    }

    private void OnSubmitButtonPressed(BaseButton.ButtonEventArgs args)
    {
        if (!ParseExpiryTime())
            return;
        if (DeleteResetOn is null)
        {
            DeleteResetOn = _gameTiming.RealTime + TimeSpan.FromSeconds(3);
            SubmitButton.Text = Loc.GetString("admin-note-editor-submit-confirm");
            SubmitButton.ModulateSelfOverride = Color.Red;
            // Task.Delay(3000).ContinueWith(_ => ResetSubmitButton()); // TODO: fix
            return;
        }

        ResetSubmitButton();

        SubmitPressed?.Invoke(NoteId, NoteType, Rope.Collapse(NoteTextEdit.TextRope), NoteSeverity, IsSecret, ExpiryTime);

        if (Parent is null)
        {
            _console.ExecuteCommand($"adminnotes \"{PlayerName}\"");
        }
        Close();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        // This checks for null for free, do not invert it as null always produces a false value
        if (DeleteResetOn < _gameTiming.RealTime)
        {
            ResetSubmitButton();
            DeleteResetOn = null;
        }
    }

    /// <summary>
    ///     Updates whether or not the submit button is disabled.
    /// </summary>
    private void UpdateSubmitButton()
    {
        if (!CanEdit)
        {
            SubmitButton.Disabled = true;
            return;
        }

        if (IsCreating && !CanCreate)
        {
            SubmitButton.Disabled = true;
            return;
        }

        SubmitButton.Disabled = (NoteType != NoteType.Watchlist && NoteType != NoteType.Message) && NoteSeverity == null;
    }

    private void ResetSubmitButton()
    {
        SubmitButton.Text = Loc.GetString("admin-note-editor-submit");
        SubmitButton.ModulateSelfOverride = null;
        UpdateDraw();
    }

    /// <summary>
    /// Tries to parse the currently entered expiry time. As a side effect this function
    /// will colour its respective line edit to indicate an error
    /// </summary>
    /// <returns>True if parsing was successful, false if not</returns>
    private bool ParseExpiryTime()
    {
        // If the checkbox is pressed the note is permanent, so expiry is null
        if (PermanentCheckBox.Pressed)
        {
            ExpiryTime = null;
            return true;
        }

        if (string.IsNullOrWhiteSpace(ExpiryLineEdit.Text) || !uint.TryParse(ExpiryLineEdit.Text, out var inputInt))
        {
            ExpiryLineEdit.ModulateSelfOverride = Color.Red;
            return false;
        }

        var mult = ExpiryLengthDropdown.SelectedId switch
        {
            (int) Multipliers.Minutes => TimeSpan.FromMinutes(1).TotalMinutes,
            (int) Multipliers.Hours => TimeSpan.FromHours(1).TotalMinutes,
            (int) Multipliers.Days => TimeSpan.FromDays(1).TotalMinutes,
            (int) Multipliers.Weeks => TimeSpan.FromDays(7).TotalMinutes,
            (int) Multipliers.Months => TimeSpan.FromDays(30).TotalMinutes,
            (int) Multipliers.Years => TimeSpan.FromDays(365).TotalMinutes,
            (int) Multipliers.Centuries => TimeSpan.FromDays(36525).TotalMinutes,
            _ => throw new ArgumentOutOfRangeException(nameof(ExpiryLengthDropdown.SelectedId), "Multiplier out of range :(")
        };
        ExpiryTime = DateTime.UtcNow.AddMinutes(inputInt * mult);
        ExpiryLineEdit.ModulateSelfOverride = null;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        PermanentCheckBox.OnPressed -= OnPermanentPressed;
        SecretCheckBox.OnPressed -= OnSecretPressed;
        SubmitButton.OnPressed -= OnSubmitButtonPressed;
        SubmitButton.OnMouseEntered -= OnSubmitButtonMouseEntered;
        SubmitButton.OnMouseExited -= OnSubmitButtonMouseExited;

        SubmitPressed = null;
    }
}
