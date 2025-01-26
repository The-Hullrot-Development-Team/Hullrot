﻿using System.Linq;
using Content.Shared.CCVar;
using Content.Shared.Decals;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Chat.ChatModifiers;

/// <summary>
/// Inserts an LoC string after a specific node. Useful for formatting certain messages, such as whispering.
/// </summary>
[Serializable]
[DataDefinition]
public sealed partial class InsertLoCChatModifier : ChatModifier
{
    /// <summary>
    /// If false, the string will be inserted before the node.
    /// </summary>
    [DataField]
    public bool AfterNode = true;

    /// <summary>
    /// The node that the string should be inserted next to.
    /// </summary>
    [DataField]
    public string? TargetNode = null;

    /// <summary>
    /// The string that should be inserted.
    /// </summary>
    [DataField]
    public string LocString = "";

    public override void ProcessChatModifier(ref FormattedMessage message, Dictionary<Enum, object> channelParameters)
    {
        if (TargetNode != null)
        {
            var str = Loc.GetString(LocString);
            if (AfterNode)
            {
                message.InsertAfterTag(new MarkupNode(str), TargetNode);
                return;
            }

            message.InsertBeforeTag(new MarkupNode(str), TargetNode);
        }
    }
}
