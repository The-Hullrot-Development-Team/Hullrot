using Robust.Shared.GameStates;
using Robust.Shared.Physics.Collision.Shapes;

namespace Content.Shared.Movement.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MobCollisionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Colliding;

    /// <summary>
    /// Shape to give this entity for mob collisions.
    /// </summary>
    [DataField, AutoNetworkedField]
    public IPhysShape Shape = new PhysShapeCircle(radius: 0.35f);

    [DataField, AutoNetworkedField]
    public float Strength = 1f;
}
