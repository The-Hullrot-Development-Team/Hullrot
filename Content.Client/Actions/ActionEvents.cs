using Content.Shared.Actions.Components;
using static Robust.Shared.Input.Binding.PointerInputCmdHandler;

namespace Content.Client.Actions;

/// <summary>
/// Client-side event used to attempt to trigger a targeted action.
/// This only gets raised if the has <see cref="TargetActionComponent">.
/// Handlers must set <c>Handled</c> to true, then if the action has been performed,
/// i.e. a target is found, then FoundTarget must be set to true.
/// </summary>
[ByRefEvent]
public record struct ActionTargetAttemptEvent(
    PointerInputCmdArgs Input,
    Entity<ActionsComponent> User,
    ActionComponent Action,
    bool Handled = false,
    bool FoundTarget = false);
