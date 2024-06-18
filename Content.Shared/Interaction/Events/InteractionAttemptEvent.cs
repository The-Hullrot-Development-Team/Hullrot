﻿namespace Content.Shared.Interaction.Events
{
    /// <summary>
    ///     Event raised directed at a user to see if they can perform a generic interaction.
    /// </summary>
    [ByRefEvent]
    public struct InteractionAttemptEvent(EntityUid uid, EntityUid? target)
    {
        public bool Cancelled;
        public EntityUid Uid = uid;
        public EntityUid? Target = target;
    }

    /// <summary>
    /// Raised to determine whether an entity is conscious to perform an action.
    /// </summary>
    public sealed class ConsciousAttemptEvent(EntityUid Uid) : CancellableEntityEventArgs
    {
        public EntityUid Uid { get; } = Uid;
    }

    /// <summary>
    ///     Event raised directed at the target entity of an interaction to see if the user is allowed to perform some
    ///     generic interaction.
    /// </summary>
    public sealed class GettingInteractedWithAttemptEvent : CancellableEntityEventArgs
    {
        public GettingInteractedWithAttemptEvent(EntityUid uid, EntityUid? target)
        {
            Uid = uid;
            Target = target;
        }

        public EntityUid Uid { get; }
        public EntityUid? Target { get; }
    }
}
