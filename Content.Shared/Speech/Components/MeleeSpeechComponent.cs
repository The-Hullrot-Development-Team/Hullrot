using Content.Shared.Actions;
using Content.Shared.Actions.ActionTypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Speech.Components;

[RegisterComponent]
[AutoGenerateComponentState]
public sealed partial class MeleeSpeechComponent : Component
{
    [ViewVariables]
    public EntityUid? User;

    [ViewVariables(VVAccess.ReadWrite)]
	[DataField("Battlecry")]
	[AutoNetworkedField]
	public string? Battlecry;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("MaxBattlecryLength")]
    public int MaxBattlecryLength = 12;

    [DataField("configureAction")]
    public InstantAction ConfigureAction = new()
    {
        UseDelay = TimeSpan.FromSeconds(4),
        ItemIconStyle = ItemActionIconStyle.BigItem,
        DisplayName = "melee-speech-config",
        Description = "melee-speech-config-desc",
        Priority = -20,
        Event = new MeleeSpeechConfigureActionEvent(),
    };
}

/// <summary>
/// Key representing which <see cref="BoundUserInterface"/> is currently open.
/// Useful when there are multiple UI for an object. Here it's future-proofing only.
/// </summary>/
[Serializable, NetSerializable]
public enum MeleeSpeechUiKey : byte
{
    Key,
}

//[Serializable, NetSerializable]
public sealed class MeleeSpeechConfigureActionEvent : InstantActionEvent
{
}

/// <summary>
/// Represents an <see cref="MeleeSpeechComponent"/> state that can be sent to the client
/// </summary>
[Serializable, NetSerializable]
public sealed class MeleeSpeechBoundUserInterfaceState : BoundUserInterfaceState
{
    public string CurrentBattlecry { get; }

    public MeleeSpeechBoundUserInterfaceState(string currentBattlecry)
    {
        CurrentBattlecry = currentBattlecry;
    }
}

[Serializable, NetSerializable]
public sealed class MeleeSpeechBattlecryChangedMessage : BoundUserInterfaceMessage
{
    public string Battlecry { get; }
    public MeleeSpeechBattlecryChangedMessage(string battlecry)
    {
        Battlecry = battlecry;
    }
}

public sealed class MeleeSpeechConfigureActionMessage : InstantActionEvent
{
    public EntityUid User { get; }

    public MeleeSpeechConfigureActionMessage(EntityUid who)
    {
        User = who;
    }
}
