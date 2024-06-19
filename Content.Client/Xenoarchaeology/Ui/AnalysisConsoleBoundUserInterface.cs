using Content.Shared.Xenoarchaeology.Equipment;
using Content.Shared.Xenoarchaeology.Equipment.Components;
using JetBrains.Annotations;

namespace Content.Client.Xenoarchaeology.Ui;

[UsedImplicitly]
public sealed class AnalysisConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private AnalysisConsoleMenu? _consoleMenu;

    protected override void Open()
    {
        base.Open();

        _consoleMenu = new AnalysisConsoleMenu();

        _consoleMenu.OnClose += Close;
        _consoleMenu.OpenCentered();

        _consoleMenu.OnServerSelectionButtonPressed += () =>
        {
            SendMessage(new AnalysisConsoleServerSelectionMessage());
        };
        _consoleMenu.OnScanButtonPressed += () =>
        {
            SendMessage(new AnalysisConsoleScanButtonPressedMessage());
        };
        _consoleMenu.OnPrintButtonPressed += () =>
        {
            SendMessage(new AnalysisConsolePrintButtonPressedMessage());
        };
        _consoleMenu.OnExtractButtonPressed += () =>
        {
            SendMessage(new AnalysisConsoleExtractButtonPressedMessage());
        };
        _consoleMenu.OnUpBiasButtonPressed += () =>
        {
            SendMessage(new AnalysisConsoleBiasButtonPressedMessage(false));
        };
        _consoleMenu.OnDownBiasButtonPressed += () =>
        {
            SendMessage(new AnalysisConsoleBiasButtonPressedMessage(true));
        };
    }

    public void Update(Entity<AnalysisConsoleComponent> ent)
    {
        _consoleMenu?.Update(ent);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _consoleMenu?.Dispose();
    }
}

