using Content.Client.UserInterface.Controls;
using Content.Shared.Botany.PlantAnalyzer;
using Content.Shared.IdentityManagement;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Botany.PlantAnalyzer;

[GenerateTypedNameReferences]
public sealed partial class PlantAnalyzerWindow : FancyWindow
{
    private readonly IEntityManager _entityManager;

    public PlantAnalyzerWindow()
    {
        RobustXamlLoader.Load(this);

        var dependencies = IoCManager.Instance!;
        _entityManager = dependencies.Resolve<IEntityManager>();
    }

    public void Populate(PlantAnalyzerScannedUserMessage msg)
    {
        var target = _entityManager.GetEntity(msg.TargetEntity);
        if (target is null)
        {
            return;
        }

        // Section 1: Icon and generic information.
        SpriteView.SetEntity(target.Value);
        SpriteView.Visible = msg.ScanMode.HasValue && msg.ScanMode.Value;
        NoDataIcon.Visible = !SpriteView.Visible;

        ScanModeLabel.Text = msg.ScanMode.HasValue
            ? msg.ScanMode.Value
                ? Loc.GetString("health-analyzer-window-scan-mode-active")
                : Loc.GetString("health-analyzer-window-scan-mode-inactive")
            : Loc.GetString("health-analyzer-window-entity-unknown-text");
        ScanModeLabel.FontColorOverride = msg.ScanMode.HasValue && msg.ScanMode.Value ? Color.Green : Color.Red;

        SeedLabel.Text = msg.SeedData == null
            ? Loc.GetString("plant-analyzer-component-no-seed")
            : Loc.GetString(msg.SeedData.DisplayName);

        ContainerLabel.Text = _entityManager.HasComponent<MetaDataComponent>(target.Value)
            ? Identity.Name(target.Value, _entityManager)
            : Loc.GetString("generic-unknown");

        // Section 2: Information regarding the tray.
        if (msg.TrayData is not null)
        {
            WaterLevelLabel.Text = msg.TrayData.WaterLevel.ToString("0.00");
            NutritionLevelLabel.Text = msg.TrayData.NutritionLevel.ToString("0.00");
            ToxinsLabel.Text = msg.TrayData.Toxins.ToString("0.00");
            PestLevelLabel.Text = msg.TrayData.PestLevel.ToString("0.00");
            WeedLevelLabel.Text = msg.TrayData.WeedLevel.ToString("0.00");
            ContainerGrid.Visible = true;
        }

        // Section 3: Information regarding the plant.
        // Label printer at the bottom (like the forensic scanner)
        // TODO: PA
    }
}
