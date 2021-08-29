using System.Linq;
using Content.Server.Ghost;
using Content.Shared.Actions.Behaviors;
using Content.Shared.Actions.Components;
using Content.Shared.Cooldown;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.Actions.Actions
{
    /// <summary>
    ///     Blink lights and scare livings
    /// </summary>
    [UsedImplicitly]
    [DataDefinition]
    public class GhostBoo : IInstantAction
    {
        [DataField("radius")] private float _radius = 3;
        [DataField("cooldown")] private float _cooldown = 120;
        [DataField("maxTargets")] private int _maxTargets = 3;

        public void DoInstantAction(InstantActionEventArgs args)
        {
            if (!args.Performer.TryGetComponent<SharedActionsComponent>(out var actions)) return;

            // find all IGhostBooAffected nearby and do boo on them
            var ents = IoCManager.Resolve<IEntityLookup>().GetEntitiesInRange(args.Performer, _radius);

            var booCounter = 0;
            foreach (var ent in ents)
            {
                var boos = ent.GetAllComponents<IGhostBooAffected>().ToList();
                foreach (var boo in boos)
                {
                    if (boo.AffectedByGhostBoo(args))
                        booCounter++;
                }

                if (booCounter >= _maxTargets)
                    break;
            }

            actions.Cooldown(args.ActionType, Cooldowns.SecondsFromNow(_cooldown));
        }
    }
}
