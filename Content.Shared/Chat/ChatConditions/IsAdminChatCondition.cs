﻿using System.Linq;
using Content.Shared.Administration.Managers;
using Robust.Shared.Player;

namespace Content.Shared.Chat.ChatConditions;

/// <summary>
/// Checks if the consumers are admins.
/// </summary>
[DataDefinition]
public sealed partial class IsAdminChatCondition : ChatCondition
{
    /// <summary>
    /// If true, deadmined sessions will be included.
    /// </summary>
    [DataField]
    public bool IncludeDeadmin;

    [Dependency] private readonly ISharedAdminManager _admin = default!;

    protected override bool Check(EntityUid subjectEntity, ChatMessageContext channelParameters)
    {
        return false;
    }

    protected override bool Check(ICommonSession subjectSession, ChatMessageContext channelParameters)
    {
        IoCManager.InjectDependencies(this);

        return _admin.IsAdmin(subjectSession, IncludeDeadmin);
    }
}
