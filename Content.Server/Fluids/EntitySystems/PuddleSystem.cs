using Content.Server.Administration.Logs;
using Content.Server.Chemistry.EntitySystems;
using Content.Server.DoAfter;
using Content.Server.Fluids.Components;
using Content.Server.Kudzu;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Solution = Content.Shared.Chemistry.Components.Solution;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Fluids.EntitySystems;

public sealed partial class PuddleSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger= default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly StepTriggerSystem _stepTrigger = default!;
    [Dependency] private readonly SolutionContainerSystem _solutionContainerSystem = default!;

    public static float PuddleVolume = 1000;

    // Using local deletion queue instead of the standard queue so that we can easily "undelete" if a puddle
    // loses & then gains reagents in a single tick.
    private HashSet<EntityUid> _deletionQueue = new();

    public const string SpreaderName = "puddle";

    /*
     * TODO: Need some sort of way to do blood slash / vomit solution spill on its own
     * This would then evaporate into the puddle tile below
     * TODO:
     * Need to re-implement evaporating solutions with water
     * TODO:
     * Mop should transfer all of its water onto a puddle.
     *
     * TODO: Edge spreader overflow stuff
     * Get neighboring should be on spreader system
     */

    public override void Initialize()
    {
        base.Initialize();

        // Shouldn't need re-anchoring.
        SubscribeLocalEvent<PuddleComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<PuddleComponent, ExaminedEvent>(HandlePuddleExamined);
        SubscribeLocalEvent<PuddleComponent, SolutionChangedEvent>(OnSolutionUpdate);
        SubscribeLocalEvent<PuddleComponent, ComponentInit>(OnPuddleInit);
        SubscribeLocalEvent<PuddleComponent, SpreadNeighborsEvent>(OnPuddleSpread);

        InitializeSpillable();
    }

    private void OnPuddleSpread(EntityUid uid, PuddleComponent component, ref SpreadNeighborsEvent args)
    {
        if (!IsOverflowing(uid, component))
            return;

        var overflow = GetOverflowSolution(uid, component);

        if (overflow.Volume == FixedPoint2.Zero)
        {
            RemCompDeferred<EdgeSpreaderComponent>(uid);
            args.Handled = true;
            return;
        }

        var xform = Transform(uid);

        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            args.Handled = true;
            return;
        }

        var pos = grid.LocalToTile(xform.Coordinates);
        var puddleQuery = GetEntityQuery<PuddleComponent>();
        Span<int> directions = stackalloc int[4]
        {
            0,
            1,
            2,
            3,
        };

        _random.Shuffle(directions);

        // First we will try to get non

        foreach (var i in directions)
        {
            var dir = (Direction) (2 * i);
            var neighbor = pos + dir.ToIntVec();

            if (!grid.TryGetTileRef(neighbor, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            // TODO: Raycasts on spreadneighbor
            var anchored = grid.GetAnchoredEntitiesEnumerator(neighbor);
            var isValid = true;

            while (anchored.MoveNext(out var neighborEnt))
            {
                if (puddleQuery.HasComponent(neighborEnt.Value))
                {
                    isValid = false;
                    break;
                }
            }

            if (!isValid)
                continue;

            TrySpillAt(tileRef, overflow, out _, false);
            return;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var ent in _deletionQueue)
        {
            Del(ent);
        }
        _deletionQueue.Clear();
    }

    private void OnPuddleInit(EntityUid uid, PuddleComponent component, ComponentInit args)
    {
        _solutionContainerSystem.EnsureSolution(uid, component.SolutionName, FixedPoint2.New(PuddleVolume), out _);
    }

    private void OnSolutionUpdate(EntityUid uid, PuddleComponent component, SolutionChangedEvent args)
    {
        if (args.Solution.Name != component.SolutionName)
            return;

        if (args.Solution.Volume <= 0)
        {
            _deletionQueue.Add(uid);
            return;
        }

        _deletionQueue.Remove(uid);
        UpdateSlip(uid, component);
        UpdateAppearance(uid, component);
    }

    private void UpdateAppearance(EntityUid uid, PuddleComponent? puddleComponent = null, AppearanceComponent? appearance = null)
    {
        if (!Resolve(uid, ref puddleComponent, ref appearance, false)
            || EmptyHolder(uid, puddleComponent))
        {
            return;
        }

        // TODO:
    }

    private void UpdateSlip(EntityUid entityUid, PuddleComponent puddleComponent)
    {
        var vol = CurrentVolume(entityUid, puddleComponent);

        if ((puddleComponent.SlipThreshold == FixedPoint2.New(-1) ||
             vol < puddleComponent.SlipThreshold) &&
            TryComp(entityUid, out StepTriggerComponent? stepTrigger))
        {
            _stepTrigger.SetActive(entityUid, false, stepTrigger);
        }
        else if (vol >= puddleComponent.SlipThreshold)
        {
            var comp = EnsureComp<StepTriggerComponent>(entityUid);
            _stepTrigger.SetActive(entityUid, true, comp);
        }
    }

    private void HandlePuddleExamined(EntityUid uid, PuddleComponent component, ExaminedEvent args)
    {
        if (TryComp<StepTriggerComponent>(uid, out var slippery) && slippery.Active)
        {
            args.PushText(Loc.GetString("puddle-component-examine-is-slipper-text"));
        }
    }

    private void OnAnchorChanged(EntityUid uid, PuddleComponent puddle, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            QueueDel(uid);
    }

    public bool EmptyHolder(EntityUid uid, PuddleComponent? puddleComponent = null)
    {
        if (!Resolve(uid, ref puddleComponent))
            return true;

        return !_solutionContainerSystem.TryGetSolution(uid, puddleComponent.SolutionName,
                   out var solution)
               || solution.Contents.Count == 0;
    }

    public FixedPoint2 CurrentVolume(EntityUid uid, PuddleComponent? puddleComponent = null)
    {
        if (!Resolve(uid, ref puddleComponent))
            return FixedPoint2.Zero;

        return _solutionContainerSystem.TryGetSolution(uid, puddleComponent.SolutionName,
            out var solution)
            ? solution.Volume
            : FixedPoint2.Zero;
    }

    /// <summary>
    /// Try to add solution to <paramref name="puddleUid"/>.
    /// </summary>
    /// <param name="puddleUid">Puddle to which we add</param>
    /// <param name="addedSolution">Solution that is added to puddleComponent</param>
    /// <param name="sound">Play sound on overflow</param>
    /// <param name="checkForOverflow">Overflow on encountered values</param>
    /// <param name="puddleComponent">Optional resolved PuddleComponent</param>
    /// <returns></returns>
    public bool TryAddSolution(EntityUid puddleUid,
        Solution addedSolution,
        bool sound = true,
        bool checkForOverflow = true,
        PuddleComponent? puddleComponent = null)
    {
        if (!Resolve(puddleUid, ref puddleComponent))
            return false;

        if (addedSolution.Volume == 0 ||
            !_solutionContainerSystem.TryGetSolution(puddleUid, puddleComponent.SolutionName,
                out var solution))
        {
            return false;
        }

        solution.AddSolution(addedSolution, _prototypeManager);
        _solutionContainerSystem.UpdateChemicals(puddleUid, solution, true);

        if (checkForOverflow && IsOverflowing(puddleUid, puddleComponent))
        {
            EnsureComp<EdgeSpreaderComponent>(puddleUid);
        }

        if (!sound)
        {
            return true;
        }

        SoundSystem.Play(puddleComponent.SpillSound.GetSound(),
            Filter.Pvs(puddleUid), puddleUid);
        return true;
    }

    /// <summary>
    ///     Whether adding this solution to this puddle would overflow.
    /// </summary>
    public bool WouldOverflow(EntityUid uid, Solution solution, PuddleComponent? puddle = null)
    {
        if (!Resolve(uid, ref puddle))
            return false;

        return CurrentVolume(uid, puddle) + solution.Volume > puddle.OverflowVolume;
    }

    /// <summary>
    ///     Whether adding this solution to this puddle would overflow.
    /// </summary>
    private bool IsOverflowing(EntityUid uid, PuddleComponent? puddle = null)
    {
        if (!Resolve(uid, ref puddle))
            return false;

        return CurrentVolume(uid, puddle) > puddle.OverflowVolume;
    }

    public Solution GetOverflowSolution(EntityUid uid, PuddleComponent? puddle = null)
    {
        if (!Resolve(uid, ref puddle) || !_solutionContainerSystem.TryGetSolution(uid, puddle.SolutionName,
                out var solution))
        {
            return new Solution(0);
        }

        // TODO: This is going to fail with struct solutions.
        var remaining = puddle.OverflowVolume * OverflowModifier;
        var split = solution.SplitSolution(CurrentVolume(uid, puddle) - remaining);
        return split;
    }

    #region Spill

    /// <summary>
    ///     Spills solution at the specified grid coordinates.
    /// </summary>
    public bool TrySpillAt(Solution solution, EntityCoordinates coordinates, out EntityUid puddleUid, bool sound = true)
    {
        if (solution.Volume == 0)
        {
            puddleUid = EntityUid.Invalid;
            return false;
        }

        if (!_mapManager.TryGetGrid(coordinates.GetGridUid(EntityManager), out var mapGrid))
        {
            puddleUid = EntityUid.Invalid;
            return false;
        }

        return TrySpillAt(mapGrid.GetTileRef(coordinates), solution, out puddleUid, sound);
    }

    /// <summary>
    ///     Spills the specified solution at the entity's location if possible.
    /// </summary>
    public bool TrySpillAt(EntityUid uid, Solution solution, out EntityUid puddleUid, bool sound = true, TransformComponent? transformComponent = null)
    {
        if (!Resolve(uid, ref transformComponent, false))
        {
            puddleUid = EntityUid.Invalid;
            return false;
        }

        return TrySpillAt(solution, transformComponent.Coordinates, out puddleUid, sound: sound);
    }

    public bool TrySpillAt(TileRef tileRef, Solution solution, out EntityUid puddleUid, bool sound = true, bool tileReact = true)
    {
        if (solution.Volume <= 0)
        {
            puddleUid = EntityUid.Invalid;
            return false;
        }

        // If space return early, let that spill go out into the void
        if (tileRef.Tile.IsEmpty)
        {
            puddleUid = EntityUid.Invalid;
            return false;
        }

        // Let's not spill to invalid grids.
        var gridId = tileRef.GridUid;
        if (!_mapManager.TryGetGrid(gridId, out var mapGrid))
        {
            puddleUid = EntityUid.Invalid;
            return false;
        }

        if (tileReact)
        {
            // First, do all tile reactions
            for (var i = 0; i < solution.Contents.Count; i++)
            {
                var (reagentId, quantity) = solution.Contents[i];
                var proto = _prototypeManager.Index<ReagentPrototype>(reagentId);
                var removed = proto.ReactionTile(tileRef, quantity);
                if (removed <= FixedPoint2.Zero)
                    continue;

                solution.RemoveReagent(reagentId, removed);
            }
        }

        // Tile reactions used up everything.
        if (solution.Volume == FixedPoint2.Zero)
        {
            puddleUid = EntityUid.Invalid;
            return false;
        }

        // Get normalized co-ordinate for spill location and spill it in the centre
        // TODO: Does SnapGrid or something else already do this?
        var anchored = mapGrid.GetAnchoredEntitiesEnumerator(tileRef.GridIndices);
        var puddleQuery = GetEntityQuery<PuddleComponent>();

        while (anchored.MoveNext(out var ent))
        {
            if (!puddleQuery.TryGetComponent(ent, out var puddle))
                continue;

            if (TryAddSolution(ent.Value, solution, sound, puddleComponent: puddle))
            {
                var spreader = EnsureComp<EdgeSpreaderComponent>(ent.Value);
                spreader.Name = SpreaderName;
            }

            puddleUid = ent.Value;
            return true;
        }

        var coords = mapGrid.GridTileToLocal(tileRef.GridIndices);
        puddleUid = EntityManager.SpawnEntity("Puddle", coords);
        EnsureComp<PuddleComponent>(puddleUid);
        if (TryAddSolution(puddleUid, solution, sound))
        {
            var spreader = EnsureComp<EdgeSpreaderComponent>(puddleUid);
            spreader.Name = SpreaderName;
        }
        return true;
    }

    #endregion
}
