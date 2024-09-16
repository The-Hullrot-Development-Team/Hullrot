using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Events;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Collections;
using Robust.Shared.Timing;

namespace Content.Server.Chemistry.EntitySystems;

/// <summary>
/// System for handling the different inheritors of <see cref="BaseSolutionInjectOnEventComponent"/>.
/// Subscribes to relevent events and performs solution injections when they are raised.
/// </summary>
public sealed class SolutionInjectWhileEmbeddedSystem : EntitySystem
{
	[Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

	public override void Update(float frameTime)
	{
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SolutionInjectWhileEmbeddedComponent, EmbeddableProjectileComponent>();
        while (query.MoveNext(out var uid, out var injectComponent, out var projectileComponent))
        {
            if (_gameTiming.CurTime < injectComponent.NextUpdate)
                continue;
            if(projectileComponent.EmbeddedIntoUid == null) {
                Console.WriteLine("Is null");
                continue;
            }

            var ev = new InjectOverTimeEvent(projectileComponent.EmbeddedIntoUid.Value);
			RaiseLocalEvent(ref ev);
            Console.WriteLine("Sent event");

            injectComponent.NextUpdate = _gameTiming.CurTime + injectComponent.UpdateInterval;
		}
	}
}
