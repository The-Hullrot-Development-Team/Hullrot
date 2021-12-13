using System;
using System.Collections.Generic;
using Content.Shared.Chemistry.Components;
using Robust.Client.AutoGenerated;
using Robust.Client.Console;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;

namespace Content.Client.Administration.UI.ManageSolutions
{
    /// <summary>
    ///     A simple window that displays solutions and their contained reagents. Allows you to edit the reagent quantities and add new reagents.
    /// </summary>
    [GenerateTypedNameReferences]
    public sealed partial class EditSolutionsWindow : SS14Window
    {
        [Dependency] private readonly IClientConsoleHost _consoleHost = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;

        private EntityUid _target = EntityUid.Invalid;
        private string? _selectedSolution;
        private AddReagentWindow? _addReagentWindow;
        private Dictionary<string, Solution>? _solutions;

        public EditSolutionsWindow()
        {
            IoCManager.InjectDependencies(this);
            RobustXamlLoader.Load(this);

            SolutionOption.OnItemSelected += SolutionSelected;
            AddButton.OnPressed += OpenAddReagentWindow;
        }

        public override void Close()
        {
            base.Close();
            _addReagentWindow?.Close();
            _addReagentWindow?.Dispose();
        }

        public void SetTargetEntity(EntityUid target)
        {
            _target = target;

            var targetName = _entityManager.EntityExists(target)
                ? IoCManager.Resolve<IEntityManager>().GetComponent<MetaDataComponent>(target).EntityName
                : string.Empty;

            Title = Loc.GetString("admin-solutions-window-title", ("targetName", targetName));
        }

        /// <summary>
        ///     Update the capacity label and re-create the reagent list
        /// </summary>
        public void UpdateReagents()
        {
            ReagentList.DisposeAllChildren();

            if (_selectedSolution == null || _solutions == null)
                return;

            if (!_solutions.TryGetValue(_selectedSolution, out var solution))
                return;

            TotalLabel.Text = Loc.GetString("admin-solutions-window-capacity-label",
                ("currentVolume", solution.TotalVolume),
                ("maxVolume",solution.MaxVolume));

            foreach (var reagent in solution)
            {
                AddReagentEntry(reagent);
            }
        }

        /// <summary>
        ///     Add a single reagent entry to the list
        /// </summary>
        private void AddReagentEntry(Solution.ReagentQuantity reagent)
        {
            var box = new BoxContainer();
            var spin = new FloatSpinBox(1, 2);

            spin.Value = reagent.Quantity.Float();
            spin.OnValueChanged += (args) => SetReagent(args, reagent.ReagentId);
            spin.HorizontalExpand = true;

            box.AddChild(new Label() { Text = reagent.ReagentId , HorizontalExpand = true});
            box.AddChild(spin);

            ReagentList.AddChild(box);
        }

        /// <summary>
        ///     Execute a command to modify the reagents in the solution.
        /// </summary>
        private void SetReagent(FloatSpinBox.FloatSpinBoxEventArgs args, string reagentId)
        {
            if (_solutions == null || _selectedSolution == null)
                return;

            var current = _solutions[_selectedSolution].GetReagentQuantity(reagentId);
            var delta = args.Value - current.Float();

            if (MathF.Abs(delta) < 0.01)
                return;

            var command = $"addreagent {_target} {_selectedSolution} {reagentId} {delta}";
            _consoleHost.ExecuteCommand(command);
        }

        /// <summary>
        ///     Open a new window that has options to add new reagents to the solution.
        /// </summary>
        private void OpenAddReagentWindow(BaseButton.ButtonEventArgs obj)
        {
            if (string.IsNullOrEmpty(_selectedSolution))
                return;

            _addReagentWindow?.Close();
            _addReagentWindow?.Dispose();

            _addReagentWindow = new AddReagentWindow(_target, _selectedSolution);
            _addReagentWindow.OpenCentered();
        }

        /// <summary>
        ///     When a new solution is selected, set _selectedSolution and update the reagent list.
        /// </summary>
        private void SolutionSelected(OptionButton.ItemSelectedEventArgs args)
        {
            SolutionOption.SelectId(args.Id);
            _selectedSolution = (string?) SolutionOption.SelectedMetadata;
            _addReagentWindow?.UpdateSolution(_selectedSolution);
            UpdateReagents();
        }

        /// <summary>
        ///     Update the solution options.
        /// </summary>
        public void UpdateSolutions(Dictionary<string, Solution>? solutions)
        {
            SolutionOption.Clear();
            _solutions = solutions;

            if (_solutions == null)
                return;

            int i = 0;
            foreach (var solution in _solutions.Keys)
            {
                SolutionOption.AddItem(solution, i);
                SolutionOption.SetItemMetadata(i, solution);

                if (solution == _selectedSolution)
                    SolutionOption.Select(i);

                i++;
            }

            if (SolutionOption.ItemCount == 0)
            {
                // No applicable solutions
                Close();
                Dispose();
            }

            if (_selectedSolution == null || !_solutions.ContainsKey(_selectedSolution))
            {
                // the previously selected solution is no longer valid.
                SolutionOption.Select(0);
                _selectedSolution = (string?) SolutionOption.SelectedMetadata;
            }
        }
    }
}
