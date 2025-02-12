using Content.Client.UserInterface.Controls;
using Content.Shared.Administration;
using Content.Shared.CriminalRecords;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.CriminalRecords;

/// <summary>
/// Window opened when Crime History button is pressed
/// </summary>
[GenerateTypedNameReferences]
public sealed partial class CrimeHistoryWindow : FancyWindow
{
    public Action<string>? OnAddHistory;
    public Action<uint>? OnDeleteHistory;

    private uint _maxLength;
    private uint? _index;
    private DialogWindow? _dialog;

    public CrimeHistoryWindow(uint maxLength)
    {
        RobustXamlLoader.Load(this);

        _maxLength = maxLength;

        OnClose += () =>
        {
            _dialog?.Close();
            // deselect so when reopening the window it doesnt try to use invalid index
            _index = null;
        };

        AddButton.OnPressed += _ =>
        {
            if (_dialog != null)
            {
                _dialog.MoveToFront();
                return;
            }

            var field = "line";
            var prompt = Loc.GetString("criminal-records-console-reason");
            var placeholder = Loc.GetString("criminal-records-history-placeholder");
            var entry = new QuickDialogEntry(field, QuickDialogEntryType.LongText, prompt, placeholder);
            var entries = new List<QuickDialogEntry> { entry };
            _dialog = new DialogWindow(Title!, entries);

            _dialog.OnConfirmed += responses =>
            {
                var line = responses[field];
                if (line.Length < 1 || line.Length > _maxLength)
                    return;

                OnAddHistory?.Invoke(line);
                // adding deselects so prevent deleting yeah
                _index = null;
                DeleteButton.Disabled = true;
            };

            // prevent MoveToFront being called on a closed window and double closing
            _dialog.OnClose += () => { _dialog = null; };
        };
        DeleteButton.OnPressed += _ =>
        {
            if (_index is not {} index)
                return;

            OnDeleteHistory?.Invoke(index);
            // prevent total spam wiping
            History.ClearSelected();
            _index = null;
            DeleteButton.Disabled = true;
        };

        History.OnItemSelected += args =>
        {
            _index = (uint) args.ItemIndex;
            DeleteButton.Disabled = false;
        };
        History.OnItemDeselected += args =>
        {
            _index = null;
            DeleteButton.Disabled = true;
        };
    }

    public void UpdateHistory(CriminalRecord record, bool access)
    {
        History.Clear();
        Editing.Visible = access;

        NoHistory.Visible = record.History.Count == 0;

        foreach (var entry in record.History)
        {
            var time = entry.AddTime;
            var line = $"{time.Hours:00}:{time.Minutes:00}:{time.Seconds:00} - {entry.Crime}";
            History.AddItem(line);
        }

        // deselect if something goes wrong
        if (_index is {} index && record.History.Count >= index)
            _index = null;
    }
}
