using Content.Server.Arcade.EntitySystems;
using Content.Shared.EntityTable.EntitySelectors;

namespace Content.Server.Arcade.Components;

/// <summary>
///
/// </summary>
[RegisterComponent, Access(typeof(ArcadeRewardsSystem))]
public sealed partial class ArcadeRewardsComponent : Component
{
    /// <summary>
    ///
    /// </summary>
    [DataField]
    public EntityTableSelector Rewards;

    /// <summary>
    ///
    /// </summary>
    [DataField]
    public int MinAmount = 0;

    /// <summary>
    ///
    /// </summary>
    [DataField]
    public int MaxAmount = 0;

    /// <summary>
    ///
    /// </summary>
    [DataField]
    public int Amount = 0;
}
