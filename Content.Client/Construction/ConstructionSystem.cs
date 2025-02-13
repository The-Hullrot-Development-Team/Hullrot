using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Popups;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Input;
using Content.Shared.Wall;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Construction
{
    /// <summary>
    /// The client-side implementation of the construction system, which is used for constructing entities in game.
    /// </summary>
    [UsedImplicitly]
    public sealed class ConstructionSystem : SharedConstructionSystem
    {
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly ExamineSystemShared _examineSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;

        private readonly Dictionary<int, EntityUid> _ghosts = new();
        private readonly Dictionary<string, ConstructionGuide> _guideCache = new();
        private Dictionary<string, string>? _recipeMetadataCache;

        public bool CraftingEnabled { get; private set; }

        public bool IsRecipesCacheWarmed { get; private set; }

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            SubscribeNetworkEvent<ResponseConstructionRecipes>(OnResponseConstructionRecipes);
            RaiseNetworkEvent(new RequestConstructionRecipes());

            UpdatesOutsidePrediction = true;
            SubscribeLocalEvent<LocalPlayerAttachedEvent>(HandlePlayerAttached);
            SubscribeNetworkEvent<AckStructureConstructionMessage>(HandleAckStructure);
            SubscribeNetworkEvent<ResponseConstructionGuide>(OnConstructionGuideReceived);

            CommandBinds.Builder
                .Bind(ContentKeyFunctions.OpenCraftingMenu,
                    new PointerInputCmdHandler(HandleOpenCraftingMenu, outsidePrediction: true))
                .Bind(EngineKeyFunctions.Use,
                    new PointerInputCmdHandler(HandleUse, outsidePrediction: true))
                .Bind(ContentKeyFunctions.EditorFlipObject,
                    new PointerInputCmdHandler(HandleFlip, outsidePrediction: true))
                .Register<ConstructionSystem>();

            SubscribeLocalEvent<ConstructionGhostComponent, ExaminedEvent>(HandleConstructionGhostExamined);
        }

        public event EventHandler? OnConstructionRecipesUpdated;
        private void OnResponseConstructionRecipes(ResponseConstructionRecipes ev)
        {
            if (_recipeMetadataCache is null)
            {
                _recipeMetadataCache = new();

                foreach (var (constructionProtoId, targetProtoId) in ev.Metadata)
                {
                    if (!_prototypeManager.TryIndex(constructionProtoId, out ConstructionPrototype? recipe))
                        continue;

                    if (!_prototypeManager.TryIndex(targetProtoId, out var proto))
                        continue;

                    var name = recipe.NameLocId.HasValue ? Loc.GetString(recipe.NameLocId) : proto.Name;
                    var desc = recipe.DescLocId.HasValue ? Loc.GetString(recipe.DescLocId) : proto.Description;

                    recipe.Name = name;
                    recipe.Description = desc;

                    _recipeMetadataCache.Add(constructionProtoId, targetProtoId);
                }

                IsRecipesCacheWarmed = true;
            }

            OnConstructionRecipesUpdated?.Invoke(this, EventArgs.Empty);
        }

        public bool TryGetRecipePrototype(string constructionProtoId, [NotNullWhen(true)] out string? targetProtoId)
        {
            if (_recipeMetadataCache is not null && _recipeMetadataCache.TryGetValue(constructionProtoId, out targetProtoId))
                return true;

            targetProtoId = null;
            return false;
        }

        private void OnConstructionGuideReceived(ResponseConstructionGuide ev)
        {
            _guideCache[ev.ConstructionId] = ev.Guide;
            ConstructionGuideAvailable?.Invoke(this, ev.ConstructionId);
        }

        /// <inheritdoc />
        public override void Shutdown()
        {
            base.Shutdown();

            CommandBinds.Unregister<ConstructionSystem>();
        }

        public ConstructionGuide? GetGuide(ConstructionPrototype prototype)
        {
            if (_guideCache.TryGetValue(prototype.ID, out var guide))
                return guide;

            RaiseNetworkEvent(new RequestConstructionGuide(prototype.ID));
            return null;
        }

        private void HandleConstructionGhostExamined(EntityUid uid, ConstructionGhostComponent component, ExaminedEvent args)
        {
            if (component.Prototype?.Name is null)
                return;

            using (args.PushGroup(nameof(ConstructionGhostComponent)))
            {
                args.PushMarkup(Loc.GetString(
                    "construction-ghost-examine-message",
                    ("name", component.Prototype.Name)));

                if (!_prototypeManager.TryIndex(component.Prototype.Graph, out ConstructionGraphPrototype? graph))
                    return;

                var startNode = graph.Nodes[component.Prototype.StartNode];

                if (!graph.TryPath(component.Prototype.StartNode, component.Prototype.TargetNode, out var path) ||
                    !startNode.TryGetEdge(path[0].Name, out var edge))
                {
                    return;
                }

                foreach (var step in edge.Steps)
                {
                    step.DoExamine(args);
                }
            }
        }

        public event EventHandler<CraftingAvailabilityChangedArgs>? CraftingAvailabilityChanged;
        public event EventHandler<string>? ConstructionGuideAvailable;
        public event EventHandler? ToggleCraftingWindow;
        public event EventHandler? FlipConstructionPrototype;

        private void HandleAckStructure(AckStructureConstructionMessage msg)
        {
            // We get sent a NetEntity but it actually corresponds to our local Entity.
            ClearGhost(msg.GhostId);
        }

        private void HandlePlayerAttached(LocalPlayerAttachedEvent msg)
        {
            var available = IsCraftingAvailable(msg.Entity);
            UpdateCraftingAvailability(available);
        }

        private bool HandleOpenCraftingMenu(in PointerInputCmdHandler.PointerInputCmdArgs args)
        {
            if (args.State == BoundKeyState.Down)
                ToggleCraftingWindow?.Invoke(this, EventArgs.Empty);
            return true;
        }

        private bool HandleFlip(in PointerInputCmdHandler.PointerInputCmdArgs args)
        {
            if (args.State == BoundKeyState.Down)
                FlipConstructionPrototype?.Invoke(this, EventArgs.Empty);
            return true;
        }

        private void UpdateCraftingAvailability(bool available)
        {
            if (CraftingEnabled == available)
                return;

            CraftingAvailabilityChanged?.Invoke(this, new CraftingAvailabilityChangedArgs(available));
            CraftingEnabled = available;
        }

        private static bool IsCraftingAvailable(EntityUid? entity)
        {
            if (entity == default)
                return false;

            // TODO: Decide if entity can craft, using capabilities or something
            return true;
        }

        private bool HandleUse(in PointerInputCmdHandler.PointerInputCmdArgs args)
        {
            if (!args.EntityUid.IsValid() || !IsClientSide(args.EntityUid))
                return false;

            if (!HasComp<ConstructionGhostComponent>(args.EntityUid))
                return false;

            TryStartConstruction(args.EntityUid);
            return true;
        }

        /// <summary>
        /// Creates a construction ghost at the given location.
        /// </summary>
        public void SpawnGhost(ConstructionPrototype prototype, EntityCoordinates loc, Direction dir)
            => TrySpawnGhost(prototype, loc, dir, out _);

        /// <summary>
        /// Creates a construction ghost at the given location.
        /// </summary>
        public bool TrySpawnGhost(
            ConstructionPrototype prototype,
            EntityCoordinates loc,
            Direction dir,
            [NotNullWhen(true)] out EntityUid? ghost)
        {
            ghost = null;
            if (_playerManager.LocalEntity is not { } user ||
                !user.IsValid())
            {
                return false;
            }

            if (!TryGetRecipePrototype(prototype.ID, out var targetProtoId) || !_prototypeManager.TryIndex(targetProtoId, out EntityPrototype? targetProto))
                return false;

            if (GhostPresent(loc))
                return false;

            var predicate = GetPredicate(prototype.CanBuildInImpassable, _transformSystem.ToMapCoordinates(loc));
            if (!_examineSystem.InRangeUnOccluded(user, loc, 20f, predicate: predicate))
                return false;

            if (!CheckConstructionConditions(prototype, loc, dir, user, showPopup: true))
                return false;

            ghost = EntityManager.SpawnEntity("constructionghost", loc);
            var comp = EntityManager.GetComponent<ConstructionGhostComponent>(ghost.Value);
            comp.Prototype = prototype;
            EntityManager.GetComponent<TransformComponent>(ghost.Value).LocalRotation = dir.ToAngle();
            _ghosts.Add(ghost.GetHashCode(), ghost.Value);

            var sprite = EntityManager.GetComponent<SpriteComponent>(ghost.Value);
            sprite.Color = new Color(48, 255, 48, 128);

            if (targetProto.TryGetComponent(out IconComponent? icon))
            {
                sprite.AddBlankLayer(0);
                sprite.LayerSetSprite(0, icon.Icon);
                sprite.LayerSetShader(0, "unshaded");
                sprite.LayerSetVisible(0, true);
            }
            else if (targetProto.Components.TryGetValue("Sprite", out _))
            {
                var dummy = EntityManager.SpawnEntity(targetProtoId, MapCoordinates.Nullspace);
                var targetSprite = EntityManager.EnsureComponent<SpriteComponent>(dummy);
                EntityManager.System<AppearanceSystem>().OnChangeData(dummy, targetSprite);

                for (var i = 0; i < targetSprite.AllLayers.Count(); i++)
                {
                    if (!targetSprite[i].Visible || !targetSprite[i].RsiState.IsValid)
                        continue;

                    var rsi = targetSprite[i].Rsi ?? targetSprite.BaseRSI;
                    if (rsi is null || !rsi.TryGetState(targetSprite[i].RsiState, out var state) ||
                        state.StateId.Name is null)
                        continue;

                    sprite.AddBlankLayer(i);
                    sprite.LayerSetSprite(i, new SpriteSpecifier.Rsi(rsi.Path, state.StateId.Name));
                    sprite.LayerSetShader(i, "unshaded");
                    sprite.LayerSetVisible(i, true);
                }

                EntityManager.DeleteEntity(dummy);
            }
            else
                return false;

            if (prototype.CanBuildInImpassable)
                EnsureComp<WallMountComponent>(ghost.Value).Arc = new(Math.Tau);

            return true;
        }

        private bool CheckConstructionConditions(ConstructionPrototype prototype, EntityCoordinates loc, Direction dir,
            EntityUid user, bool showPopup = false)
        {
            foreach (var condition in prototype.Conditions)
            {
                if (!condition.Condition(user, loc, dir))
                {
                    if (showPopup)
                    {
                        var message = condition.GenerateGuideEntry()?.Localization;
                        if (message != null)
                        {
                            // Show the reason to the user:
                            _popupSystem.PopupCoordinates(Loc.GetString(message), loc);
                        }
                    }

                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if any construction ghosts are present at the given position
        /// </summary>
        private bool GhostPresent(EntityCoordinates loc)
        {
            foreach (var ghost in _ghosts)
            {
                if (EntityManager.GetComponent<TransformComponent>(ghost.Value).Coordinates.Equals(loc))
                    return true;
            }

            return false;
        }

        public void TryStartConstruction(EntityUid ghostId, ConstructionGhostComponent? ghostComp = null)
        {
            if (!Resolve(ghostId, ref ghostComp))
                return;

            if (ghostComp.Prototype == null)
            {
                throw new ArgumentException($"Can't start construction for a ghost with no prototype. Ghost id: {ghostId}");
            }

            var transform = EntityManager.GetComponent<TransformComponent>(ghostId);
            var msg = new TryStartStructureConstructionMessage(GetNetCoordinates(transform.Coordinates), ghostComp.Prototype.ID, transform.LocalRotation, ghostId.GetHashCode());
            RaiseNetworkEvent(msg);
        }

        /// <summary>
        /// Starts constructing an item underneath the attached entity.
        /// </summary>
        public void TryStartItemConstruction(string prototypeName)
        {
            RaiseNetworkEvent(new TryStartItemConstructionMessage(prototypeName));
        }

        /// <summary>
        /// Removes a construction ghost entity with the given ID.
        /// </summary>
        public void ClearGhost(int ghostId)
        {
            if (!_ghosts.TryGetValue(ghostId, out var ghost))
                return;

            EntityManager.QueueDeleteEntity(ghost);
            _ghosts.Remove(ghostId);
        }

        /// <summary>
        /// Removes all construction ghosts.
        /// </summary>
        public void ClearAllGhosts()
        {
            foreach (var ghost in _ghosts.Values)
            {
                EntityManager.QueueDeleteEntity(ghost);
            }

            _ghosts.Clear();
        }
    }

    public sealed class CraftingAvailabilityChangedArgs : EventArgs
    {
        public bool Available { get; }

        public CraftingAvailabilityChangedArgs(bool available)
        {
            Available = available;
        }
    }
}
