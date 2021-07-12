#nullable enable
using System;
using System.Threading.Tasks;
using Content.Server.UserInterface;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Solution.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Helpers;
using Content.Shared.Notification.Managers;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Server.Chemistry.Components
{
    /// <summary>
    ///     Gives click behavior for transferring to/from other reagent containers.
    /// </summary>
    [RegisterComponent]
    public sealed class SolutionTransferComponent : Component, IAfterInteract
    {
        // Behavior is as such:
        // If it's a reagent tank, TAKE reagent.
        // If it's anything else, GIVE reagent.
        // Of course, only if possible.

        public override string Name => "SolutionTransfer";

        /// <summary>
        ///     The amount of solution to be transferred from this solution when clicking on other solutions with it.
        /// </summary>
        [DataField("transferAmount")]
        [ViewVariables(VVAccess.ReadWrite)]
        public ReagentUnit TransferAmount { get; set; } = ReagentUnit.New(5);

        /// <summary>
        ///     The minimum amount of solution that can be transferred at once from this solution.
        /// </summary>
        [DataField("minTransferAmount")]
        [ViewVariables(VVAccess.ReadWrite)]
        public ReagentUnit MinimumTransferAmount { get; set; } = ReagentUnit.New(5);

        /// <summary>
        ///     The maximum amount of solution that can be transferred at once from this solution.
        /// </summary>
        [DataField("maxTransferAmount")]
        [ViewVariables(VVAccess.ReadWrite)]
        public ReagentUnit MaximumTransferAmount { get; set; } = ReagentUnit.New(50);

        /// <summary>
        ///     Can this entity take reagent from reagent tanks?
        /// </summary>
        [DataField("canReceive")]
        [ViewVariables(VVAccess.ReadWrite)]
        public bool CanReceive { get; set; } = true;

        /// <summary>
        ///     Can this entity give reagent to other reagent containers?
        /// </summary>
        [DataField("canSend")]
        [ViewVariables(VVAccess.ReadWrite)]
        public bool CanSend { get; set; } = true;

        /// <summary>
        /// Whether you're allowed to change the transfer amount.
        /// </summary>
        [DataField("canChangeTransferAmount")]
        [ViewVariables(VVAccess.ReadWrite)]
        public bool CanChangeTransferAmount { get; set; } = false;

        [ViewVariables] private BoundUserInterface? UserInterface => Owner.GetUIOrNull(TransferAmountUiKey.Key);

        protected override void Initialize()
        {
            base.Initialize();

            if (UserInterface != null)
            {
                UserInterface.OnReceiveMessage += UserInterfaceOnReceiveMessage;
            }
        }

        public void UserInterfaceOnReceiveMessage(ServerBoundUserInterfaceMessage serverMsg)
        {
            switch (serverMsg.Message)
            {
                case TransferAmountSetValueMessage svm:
                    var sval = svm.Value.Float();
                    var amount = Math.Clamp(sval, MinimumTransferAmount.Float(),
                        MaximumTransferAmount.Float());

                    serverMsg.Session.AttachedEntity?.PopupMessage(Loc.GetString("comp-solution-transfer-set-amount", ("amount", amount)));
                    SetTransferAmount(ReagentUnit.New(amount));
                    break;
            }
        }

        public void SetTransferAmount(ReagentUnit amount)
        {
            amount = ReagentUnit.New(Math.Clamp(amount.Int(), MinimumTransferAmount.Int(), MaximumTransferAmount.Int()));
            TransferAmount = amount;
        }

        async Task<bool> IAfterInteract.AfterInteract(AfterInteractEventArgs eventArgs)
        {
            if (!eventArgs.InRangeUnobstructed() || eventArgs.Target == null)
                return false;

            if (!Owner.TryGetComponent(out ISolutionInteractionsComponent? ownerSolution))
                return false;

            var target = eventArgs.Target;
            if (!target.TryGetComponent(out ISolutionInteractionsComponent? targetSolution))
            {
                return false;
            }

            if (CanReceive && target.TryGetComponent(out ReagentTankComponent? tank)
                           && ownerSolution.CanRefill && targetSolution.CanDrain)
            {
                var transferred = DoTransfer(targetSolution, ownerSolution, tank.TransferAmount, eventArgs.User);
                if (transferred > 0)
                {
                    var toTheBrim = ownerSolution.RefillSpaceAvailable == 0;
                    var msg = toTheBrim
                        ? "comp-solution-transfer-fill-fully"
                        : "comp-solution-transfer-fill-normal";

                    target.PopupMessage(eventArgs.User, Loc.GetString(msg,("owner", Owner),("amount", transferred),("target", target)));
                    return true;
                }
            }

            if (CanSend && targetSolution.CanRefill && ownerSolution.CanDrain)
            {
                var transferred = DoTransfer(ownerSolution, targetSolution, TransferAmount, eventArgs.User);

                if (transferred > 0)
                {
                    Owner.PopupMessage(eventArgs.User,
                                       Loc.GetString("comp-solution-transfer-transfer-solution",
                                                     ("amount",transferred),
                                                     ("target",target)));

                    return true;
                }
            }

            return true;
        }

        /// <returns>The actual amount transferred.</returns>
        private static ReagentUnit DoTransfer(
            ISolutionInteractionsComponent source,
            ISolutionInteractionsComponent target,
            ReagentUnit amount,
            IEntity user)
        {
            if (source.DrainAvailable == 0)
            {
                source.Owner.PopupMessage(user, Loc.GetString("comp-solution-transfer-is-empty", ("target", source.Owner)));
                return ReagentUnit.Zero;
            }

            if (target.RefillSpaceAvailable == 0)
            {
                target.Owner.PopupMessage(user, Loc.GetString("comp-solution-transfer-is-full", ("target", target.Owner)));
                return ReagentUnit.Zero;
            }

            var actualAmount =
                ReagentUnit.Min(amount, ReagentUnit.Min(source.DrainAvailable, target.RefillSpaceAvailable));

            var solution = source.Drain(actualAmount);
            target.Refill(solution);

            return actualAmount;
        }
    }
}
