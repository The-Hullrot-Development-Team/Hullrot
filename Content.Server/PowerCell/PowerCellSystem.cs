﻿using Content.Server.Chemistry.Components;
using Content.Server.PowerCell.Components;
using Content.Shared.Chemistry;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;

namespace Content.Server.PowerCell
{
    [UsedImplicitly]
    public class PowerCellSystem  : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<PowerCellComponent, SolutionChangeEvent>(OnSolutionChange);
        }

        private void OnSolutionChange(EntityUid uid, PowerCellComponent component, SolutionChangeEvent args)
        {
            component.IsRigged = args.Owner.TryGetComponent(out SolutionContainerComponent? solution)
                                && solution.Solution.ContainsReagent("Plasma", out var plasma)
                                && plasma >= 5;
        }
    }
}
