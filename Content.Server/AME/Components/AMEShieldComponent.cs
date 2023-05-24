﻿using Content.Server.AME.EntitySystems;
using Content.Shared.AME;

namespace Content.Server.AME.Components;

/// <summary>
/// The component used to make an entity part of the bulk machinery of an AntiMatter Engine.
/// Connects to adjacent entities with this component or <see cref="AmeControllerComponent"/> to make an AME.
/// </summary>
[Access(typeof(AmeShieldingSystem), typeof(AmeNodeGroup))]
[RegisterComponent]
public sealed class AmeShieldComponent : SharedAMEShieldComponent
{
    /// <summary>
    /// Whether or not this AME shield counts as a core for the AME or not.
    /// </summary>
    [ViewVariables]
    public bool IsCore = false;

    /// <summary>
    /// The current integrity of the AME shield.
    /// </summary>
    [DataField("integrity")]
    [ViewVariables]
    public int CoreIntegrity = 100;
}
