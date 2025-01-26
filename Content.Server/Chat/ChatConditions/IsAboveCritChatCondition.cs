﻿using System.Linq;
using Content.Server.Power.Components;
using Content.Server.Radio.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Mobs.Systems;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Microsoft.Extensions.Logging;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Chat.ChatConditions;

/// <summary>
/// Checks if the consumer is alive and above crit; does not check for consciousness e.g. sleeping.
/// </summary>
[DataDefinition]
public sealed partial class IsAboveCritChatCondition : ChatCondition
{

    [Dependency] private readonly IEntitySystemManager _entitySystem = default!;

    protected override bool Check(EntityUid subjectEntity, ChatMessageContext channelParameters)
    {
        IoCManager.InjectDependencies(this);

        if (_entitySystem.TryGetEntitySystem<MobStateSystem>(out var mobStateSystem))
        {
            return mobStateSystem.IsIncapacitated(subjectEntity);
        }
        return false;
    }

    protected override bool Check(ICommonSession subjectSession, ChatMessageContext channelParameters)
    {
        return false;
    }
}
