﻿namespace Content.Server.Roles;

/// <summary>
/// Adds a briefing to the character info menu, does nothing else.
/// </summary>
[RegisterComponent]
public sealed partial class RoleBriefingComponent : Component
{
    public string Briefing;
}
