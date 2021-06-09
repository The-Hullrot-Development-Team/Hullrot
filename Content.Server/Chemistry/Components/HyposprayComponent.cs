using Content.Server.Interaction.Components;
using Content.Server.MobState.States;
using Content.Server.Weapon.Melee;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Notification;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Players;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Server.Chemistry.Components
{
    [RegisterComponent]
    public sealed class HyposprayComponent : SharedHyposprayComponent, ISolutionChange
    {
        [DataField("ClumsyFailChance")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float ClumsyFailChance { get; set; } = 0.5f;

        [DataField("TransferAmount")]
        [ViewVariables(VVAccess.ReadWrite)]
        public ReagentUnit TransferAmount { get; set; } = ReagentUnit.New(5);

        [ComponentDependency] private readonly SolutionContainerComponent? _solution = default!;

        public override void Initialize()
        {
            base.Initialize();

            Dirty();
        }

        public bool TryDoInject(IEntity? target, IEntity user)
        {
            if (target == null || !EligibleEntity(target))
                return false;

            var msgFormat = "You inject {0:TheName}.";

            if (target == user)
            {
                msgFormat = "You inject yourself.";
            }
            else if (EligibleEntity(user) && ClumsyComponent.TryRollClumsy(user, ClumsyFailChance))
            {
                msgFormat = "Oops! You injected yourself!";
                target = user;
            }

            if (_solution == null || _solution.CurrentVolume == 0)
            {
                user.PopupMessageCursor(Loc.GetString("It's empty!"));
                return true;
            }

            user.PopupMessage(Loc.GetString(msgFormat, target));
            if (target != user)
            {
                target.PopupMessage(Loc.GetString("You feel a tiny prick!"));
                var meleeSys = EntitySystem.Get<MeleeWeaponSystem>();
                var angle = Angle.FromWorldVec(target.Transform.WorldPosition - user.Transform.WorldPosition);
                meleeSys.SendLunge(angle, user);
            }

            SoundSystem.Play(Filter.Pvs(user), "/Audio/Items/hypospray.ogg", user);

            var targetSolution = target.GetComponent<SolutionContainerComponent>();

            // Get transfer amount. May be smaller than _transferAmount if not enough room
            var realTransferAmount = ReagentUnit.Min(TransferAmount, targetSolution.EmptyVolume);

            if (realTransferAmount <= 0)
            {
                user.PopupMessage(user, Loc.GetString("{0:TheName} is already full!", targetSolution.Owner));
                return true;
            }

            // Move units from attackSolution to targetSolution
            var removedSolution = _solution.SplitSolution(realTransferAmount);

            if (!targetSolution.CanAddSolution(removedSolution))
            {
                return true;
            }

            removedSolution.DoEntityReaction(target, ReactionMethod.Injection);

            targetSolution.TryAddSolution(removedSolution);

            static bool EligibleEntity(IEntity entity)
            {
                // TODO: Does checking for BodyComponent make sense as a "can be hypospray'd" tag?
                // In SS13 the hypospray ONLY works on mobs, NOT beakers or anything else.
                return entity.HasComponent<SolutionContainerComponent>() && entity.HasComponent<MobStateComponent>();
            }

            return true;
        }

        void ISolutionChange.SolutionChanged(SolutionChangeEventArgs eventArgs)
        {
            Dirty();
        }

        public override ComponentState GetComponentState(ICommonSession player)
        {
            if (_solution == null)
                return new HyposprayComponentState(ReagentUnit.Zero, ReagentUnit.Zero);

            return new HyposprayComponentState(_solution.CurrentVolume, _solution.MaxVolume);
        }
    }
}
