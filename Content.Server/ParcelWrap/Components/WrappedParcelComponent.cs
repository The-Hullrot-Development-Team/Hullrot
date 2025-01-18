using Content.Server.ParcelWrap.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.ParcelWrap.Components;

/// <summary>
/// This component marks its owner as being a parcel created by wrapping another item up. It can be unwrapped,
/// destroying this entity and releasing <see cref="Contents"/>.
/// </summary>
/// <seealso cref="ParcelWrapComponent"/>
[RegisterComponent, Access(typeof(ParcelWrappingSystem))]
public sealed partial class WrappedParcelComponent : Component
{
    /// <summary>
    /// The contents of this parcel.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public ContainerSlot Contents = default!;

    /// <summary>
    /// Specifies the entity to spawn when this parcel is unwrapped.
    /// </summary>
    [DataField, ViewVariables]
    public ProtoId<EntityPrototype>? UnwrapTrash;

    /// <summary>
    /// Sound played when unwrapping this parcel.
    /// </summary>
    [DataField, ViewVariables]
    public SoundSpecifier? UnwrapSound;

    /// <summary>
    /// The ID of <see cref="Contents"/>.
    /// </summary>
    public const string ContainerId = "contents";
}
