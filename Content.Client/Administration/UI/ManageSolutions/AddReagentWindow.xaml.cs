using Content.Shared.Chemistry.Reagent;
using Robust.Client.AutoGenerated;
using Robust.Client.Console;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Client.Administration.UI.ManageSolutions
{
    [GenerateTypedNameReferences]
    public sealed partial class AddReagentWindow : SS14Window
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IClientConsoleHost _consoleHost = default!;

        private readonly EntityUid _targetEntity;
        private string _targetSolution;
        private ReagentPrototype? _selectedReagent;

        public AddReagentWindow(EntityUid targetEntity, string targetSolution)
        {
            IoCManager.InjectDependencies(this);
            RobustXamlLoader.Load(this);

            Title = Loc.GetString("admin-add-reagent-window-title", ("solution", targetSolution));

            _targetEntity = targetEntity;
            _targetSolution = targetSolution;

            ReagentList.OnItemSelected += ReagentListSelected;
            ReagentList.OnItemDeselected += ReagentListDeselected;
            SearchBar.OnTextChanged += SearchTextChanged;
            QuantitySpin.OnValueChanged += QuantityChanged;
            AddButton.OnPressed += AddReagent;

            UpdateReagentPrototypes();
            UpdateAddButton();
        }

        private void QuantityChanged(FloatSpinBox.FloatSpinBoxEventArgs obj)
        {
            UpdateAddButton();
        }

        private void AddReagent(BaseButton.ButtonEventArgs obj)
        {
            if (_selectedReagent == null)
                return;

            var command = $"addreagent {_targetEntity} {_targetSolution} {_selectedReagent.ID} {QuantitySpin.Value}";
            _consoleHost.ExecuteCommand(command);
        }

        private void ReagentListSelected(ItemList.ItemListSelectedEventArgs obj)
        {
            _selectedReagent = (ReagentPrototype) obj.ItemList[obj.ItemIndex].Metadata!;
            UpdateAddButton();
        }

        public void UpdateSolution(string? selectedSolution)
        {
            if (selectedSolution == null)
            {
                Close();
                Dispose();
                return;
            }

            _targetSolution = selectedSolution;
            Title = Loc.GetString("admin-add-reagent-window-title", ("solution", _targetSolution));
            UpdateAddButton();
        }

        private void UpdateAddButton()
        {
            AddButton.Disabled = true;
            if (_selectedReagent == null)
            {
                AddButton.Text = Loc.GetString("admin-add-reagent-window-add-invalid-reagent");
                return;
            }

            AddButton.Text = Loc.GetString("admin-add-reagent-window-add",
                ("quantity", QuantitySpin.Value.ToString("F2")),
                ("reagent", _selectedReagent.ID));

            AddButton.Disabled = false;
        }

        private void ReagentListDeselected(ItemList.ItemListDeselectedEventArgs obj)
        {
            _selectedReagent = null;
            UpdateAddButton();
        }

        private void SearchTextChanged(LineEdit.LineEditEventArgs obj)
        {
            UpdateReagentPrototypes(SearchBar.Text);
        }

        private void UpdateReagentPrototypes(string? filter = null)
        {
            ReagentList.Clear();
            foreach (var reagent in _prototypeManager.EnumeratePrototypes<ReagentPrototype>())
            {
                if (!string.IsNullOrEmpty(filter) &&
                   !reagent.ID.ToLowerInvariant().Contains(filter.Trim().ToLowerInvariant()))
                {
                    continue;
                }

                ItemList.Item regentItem = new(ReagentList)
                {
                    Metadata = reagent,
                    Text = reagent.ID
                };

                ReagentList.Add(regentItem);
            }
        }
    }
}
