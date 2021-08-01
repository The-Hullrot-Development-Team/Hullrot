using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Solution.Components;
using Content.Shared.Examine;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry
{
    /// <summary>
    /// This event alerts system that the solution was changed
    /// </summary>
    public class SolutionChangeEvent : EntityEventArgs
    {
        public IEntity Owner { get; }

        public SolutionChangeEvent(IEntity owner)
        {
            Owner = owner;
        }
    }

    [UsedImplicitly]
    public class ChemistrySystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SharedSolutionContainerComponent, ExaminedEvent>(OnExamineSolution);
        }

        private void OnExamineSolution(EntityUid uid, SharedSolutionContainerComponent component, ExaminedEvent args)
        {
            if (!component.CanExamineContents)
                return;

            if (component.ReagentList.Count == 0)
            {
                args.Message.AddText(Loc.GetString("shared-solution-container-component-on-examine-empty-container"));
                return;
            }

            var primaryReagent = component.Solution.GetPrimaryReagentId();
            if (!_prototypeManager.TryIndex(primaryReagent, out ReagentPrototype? proto))
            {
                Logger.Error(
                    $"{nameof(SharedSolutionContainerComponent)} could not find the prototype associated with {primaryReagent}.");
                return;
            }

            var colorHex =
                component.Color
                    .ToHexNoAlpha(); //TODO: If the chem has a dark color, the examine text becomes black on a black background, which is unreadable.
            var messageString = "shared-solution-container-component-on-examine-main-text";

            args.Message.AddMarkup(Loc.GetString(messageString,
                ("color", colorHex),
                ("wordedAmount", Loc.GetString(component.ReagentList.Count == 1
                    ? "shared-solution-container-component-on-examine-worded-amount-one-reagent"
                    : "shared-solution-container-component-on-examine-worded-amount-multiple-reagents")),
                ("desc", Loc.GetString(proto.PhysicalDescription))));
        }


        public void ReactionEntity(IEntity? entity, ReactionMethod method, string reagentId, ReagentUnit reactVolume,
            Solution.Solution? source)
        {
            // We throw if the reagent specified doesn't exist.
            ReactionEntity(entity, method, _prototypeManager.Index<ReagentPrototype>(reagentId), reactVolume, source);
        }

        public void ReactionEntity(IEntity? entity, ReactionMethod method, ReagentPrototype reagent,
            ReagentUnit reactVolume, Solution.Solution? source)
        {
            if (entity == null || entity.Deleted || !entity.TryGetComponent(out ReactiveComponent? reactive))
                return;

            foreach (var reaction in reactive.Reactions)
            {
                // If we have a source solution, use the reagent quantity we have left. Otherwise, use the reaction volume specified.
                reaction.React(method, entity, reagent, source?.GetReagentQuantity(reagent.ID) ?? reactVolume, source);

                // Make sure we still have enough reagent to go...
                if (source != null && !source.ContainsReagent(reagent.ID))
                    break;
            }
        }
    }
}
