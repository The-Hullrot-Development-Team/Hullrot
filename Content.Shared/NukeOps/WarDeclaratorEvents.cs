﻿using Robust.Shared.Serialization;

namespace Content.Shared.NukeOps;

[Serializable, NetSerializable]
public enum WarDeclaratorUiKey
{
    Key,
}

public enum WarConditionStatus : byte
{
    WarReady,
    YesWar,
    NoWarUnknown,
    NoWarTimeout,
    NoWarSmallCrew,
    NoWarShuttleDeparted
}

[Serializable, NetSerializable]
public sealed class WarDeclaratorBoundUserInterfaceState : BoundUserInterfaceState
{
    public WarConditionStatus? Status;
    public TimeSpan EndTime;

    public WarDeclaratorBoundUserInterfaceState(WarConditionStatus? status, TimeSpan endTime)
    {
        Status = status;
        EndTime = endTime;
    }
}

[Serializable, NetSerializable]
public sealed class WarDeclaratorActivateMessage : BoundUserInterfaceMessage
{
    public string Message { get; }

    public WarDeclaratorActivateMessage(string msg)
    {
        Message = msg;
    }
}
