﻿using Content.Shared.Mind;

namespace Content.Shared.Roles;

/// <summary>
///     Base event raised on player entities to indicate that something changed about one of their roles.
/// </summary>
/// <param name="MindId">The mind id associated with the player.</param>
/// <param name="Mind">The mind component associated with the mind id.</param>
/// <param name="RoleTypeUpdate">True if this update has changed the mind's role type</param>
public abstract record RoleEvent(EntityUid MindId, MindComponent Mind, bool RoleTypeUpdate);
