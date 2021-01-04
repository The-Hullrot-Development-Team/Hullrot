using Content.Server.GameObjects.Components.Mobs;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Serialization;

namespace Content.Server.Jobs
{
    // Used by clown job def.
    [UsedImplicitly]
    public sealed class ClownSpecial : JobSpecial
    {
        public override void AfterEquip(IEntity mob)
        {
            base.AfterEquip(mob);

            mob.AddComponent<ClumsyComponent>();
        }

        public override IDeepClone DeepClone()
        {
            return new ClownSpecial();
        }
    }
}
