using Content.Shared.ActionBlocker;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.Components
{
    /// <summary>
    /// Stores what shuttle this entity is currently piloting.
    /// </summary>
    [RegisterComponent]
    [NetworkedComponent]
    public sealed class PilotComponent : Component
    {
        [ViewVariables] public SharedShuttleConsoleComponent? Console { get; set; }

        /// <summary>
        /// Where we started piloting from to check if we should break from moving too far.
        /// </summary>
        [ViewVariables] public EntityCoordinates? Position { get; set; }

        public const float BreakDistance = 0.25f;

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);
            if (curState is not PilotComponentState state) return;

            var console = state.Console.GetValueOrDefault();
            if (!console.IsValid())
            {
                Console = null;
                EntitySystem.Get<ActionBlockerSystem>().RefreshCanMove(Owner);
                return;
            }

            var entityManager = IoCManager.Resolve<IEntityManager>();

            if (!entityManager.TryGetComponent(console, out SharedShuttleConsoleComponent? shuttleConsoleComponent))
            {
                Logger.Warning($"Unable to set Helmsman console to {console}");
                return;
            }

            Console = shuttleConsoleComponent;
            EntitySystem.Get<ActionBlockerSystem>().RefreshCanMove(Owner);
        }

        public override ComponentState GetComponentState()
        {
            return Console == null ? new PilotComponentState(null) : new PilotComponentState(Console.Owner);
        }

        [Serializable, NetSerializable]
        private sealed class PilotComponentState : ComponentState
        {
            public EntityUid? Console { get; }

            public PilotComponentState(EntityUid? uid)
            {
                Console = uid;
            }
        }
    }
}
