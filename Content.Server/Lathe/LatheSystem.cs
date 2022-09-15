using System.Diagnostics.CodeAnalysis;
using Content.Server.Lathe.Components;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Content.Server.Research.Components;
using Content.Server.Research;
using Content.Shared.Research.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using JetBrains.Annotations;
using System.Linq;
using Content.Server.Cargo.Systems;
using Content.Server.Power.Components;
using Content.Server.UserInterface;
using Robust.Server.Player;

namespace Content.Server.Lathe
{
    [UsedImplicitly]
    public sealed class LatheSystem : SharedLatheSystem
    {
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSys = default!;
        [Dependency] private readonly ResearchSystem _researchSys = default!;
        [Dependency] private readonly MaterialStorageSystem _materialStorage = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<LatheComponent, MaterialEntityInsertedEvent>(OnMaterialEntityInserted);
            SubscribeLocalEvent<LatheComponent, GetMaterialWhitelistEvent>(OnGetWhitelist);
            SubscribeLocalEvent<LatheComponent, MapInitEvent>(OnMapInit);

            SubscribeLocalEvent<LatheComponent, LatheQueueRecipeMessage>(OnLatheQueueRecipeMessage);
            SubscribeLocalEvent<LatheComponent, LatheSyncRequestMessage>(OnLatheSyncRequestMessage);
            SubscribeLocalEvent<LatheComponent, LatheServerSelectionMessage>(OnLatheServerSelectionMessage);

            SubscribeLocalEvent<LatheComponent, BeforeActivatableUIOpenEvent>((u,c,_) => UpdateUserInterfaceState(u,c));
            SubscribeLocalEvent<LatheComponent, MaterialAmountChangedEvent>((u,c,_) => UpdateUserInterfaceState(u,c));

            SubscribeLocalEvent<LatheComponent, PowerChangedEvent>(OnPowerChanged);

            SubscribeLocalEvent<TechnologyDatabaseComponent, LatheGetRecipesEvent>(OnGetRecipes);
            SubscribeLocalEvent<TechnologyDatabaseComponent, LatheServerSyncMessage>(OnLatheServerSyncMessage);
        }

        public override void Update(float frameTime)
        {
            foreach (var comp in EntityQuery<LatheInsertingComponent>())
            {
                comp.TimeRemaining -= frameTime;

                if (comp.TimeRemaining > 0)
                    continue;

                UpdateInsertingAppearance(comp.Owner, false);
                RemCompDeferred(comp.Owner, comp);
            }

            foreach (var comp in EntityQuery<LatheComponent>())
            {
                if (comp.Queue.Count <= 0)
                    continue;

                TryStartProducing(comp.Owner, comp);
            }

            foreach (var (comp, lathe) in EntityQuery<LatheProducingComponent, LatheComponent>())
            {
                if (lathe.Recipe == null)
                    continue;

                comp.TimeRemaining += frameTime;

                if (comp.TimeRemaining >= (float) lathe.Recipe.CompleteTime.TotalSeconds)
                    FinishProducing(comp.Owner, lathe);
            }
        }

        private void OnGetWhitelist(EntityUid uid, LatheComponent component, GetMaterialWhitelistEvent args)
        {
            if (args.Storage != uid)
                return;
            var materialWhitelist = new List<string>();
            var recipes =  GetAllBaseRecipes(component);
            foreach (var id in recipes)
            {
                if (!_proto.TryIndex<LatheRecipePrototype>(id, out var proto))
                    continue;
                foreach (var (mat, _) in proto.RequiredMaterials)
                {
                    if (!materialWhitelist.Contains(mat))
                    {
                        materialWhitelist.Add(mat);
                    }
                }
            }

            var combined = args.Whitelist.Union(materialWhitelist).ToList();
            args.Whitelist = combined;
        }

        [PublicAPI]
        public bool TryGetAvailableRecipes(EntityUid uid, [NotNullWhen(true)] out List<string>? recipes, LatheComponent? component = null)
        {
            recipes = null;
            if (!Resolve(uid, ref component))
                return false;
            recipes = GetAvailableRecipes(component);
            return true;
        }

        public List<string> GetAvailableRecipes(LatheComponent component)
        {
            var ev = new LatheGetRecipesEvent(component.Owner)
            {
                Recipes = component.StaticRecipes
            };
            RaiseLocalEvent(component.Owner, ev);
            return ev.Recipes;
        }

        public List<string> GetAllBaseRecipes(LatheComponent component)
        {
            return component.DynamicRecipes == null
                ? component.StaticRecipes
                : component.StaticRecipes.Union(component.DynamicRecipes).ToList();
        }

        public bool TryAddToQueue(EntityUid uid, LatheRecipePrototype recipe, LatheComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return false;

            if (!CanProduce(uid, recipe, 1, component))
                return false;

            foreach (var (mat, amount) in recipe.RequiredMaterials)
            {
                _materialStorage.TryChangeMaterialAmount(uid, mat, -amount);
            }
            component.Queue.Add(recipe);

            return true;
        }

        public bool TryStartProducing(EntityUid uid, LatheComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return false;
            if (component.Recipe != null || component.Queue.Count <= 0)
                return false;

            var recipe = component.Queue.First();
            component.Queue.RemoveAt(0);

            AddComp<LatheProducingComponent>(uid);
            component.Recipe = recipe;

            _audio.PlayPvs(component.ProducingSound, component.Owner);
            UpdateRunningAppearance(uid, true);
            UpdateUserInterfaceState(uid, component);
            return true;
        }

        public void FinishProducing(EntityUid uid, LatheComponent? comp = null, LatheProducingComponent? prodComp = null)
        {
            if (!Resolve(uid, ref comp, ref prodComp, false))
                return;

            if (comp.Recipe != null)
                Spawn(comp.Recipe.Result, Transform(uid).Coordinates);
            RemComp(prodComp.Owner, prodComp);
            comp.Recipe = null;
            UpdateRunningAppearance(uid, false);
            UpdateUserInterfaceState(uid, comp);
        }

        public void UpdateUserInterfaceState(EntityUid uid, LatheComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            var ui = _uiSys.GetUi(uid, LatheUiKey.Key);
            var producing = component.Recipe ?? component.Queue.FirstOrDefault();

            var state = new LatheUpdateState(GetAvailableRecipes(component), component.Queue, producing);
            _uiSys.SetUiState(ui, state);
        }

        private void OnGetRecipes(EntityUid uid, TechnologyDatabaseComponent component, LatheGetRecipesEvent args)
        {
            if (uid != args.Lathe || !TryComp<LatheComponent>(uid, out var latheComponent) || latheComponent.DynamicRecipes == null)
                return;

            //gets all of the techs that are unlocked and also in the DynamicRecipes list
            var allTechs = (from tech in component.Technologies
                from recipe in tech.UnlockedRecipes
                where latheComponent.DynamicRecipes.Contains(recipe)
                select recipe).ToList();

            args.Recipes = args.Recipes.Union(allTechs).ToList();
        }

        /// <summary>
        /// Initialize the UI and appearance.
        /// Appearance requires initialization or the layers break
        /// </summary>
        private void OnMapInit(EntityUid uid, LatheComponent component, MapInitEvent args)
        {
            _appearance.SetData(uid, LatheVisuals.IsInserting, false);
            _appearance.SetData(uid, LatheVisuals.IsRunning, false);

            _materialStorage.UpdateMaterialWhitelist(uid);
        }

        private void OnMaterialEntityInserted(EntityUid uid, LatheComponent component, MaterialEntityInsertedEvent args)
        {
            var lastMat = args.Materials.Keys.Last();
            // We need the prototype to get the color
            _proto.TryIndex(lastMat, out MaterialPrototype? matProto);
            EnsureComp<LatheInsertingComponent>(uid).TimeRemaining = component.InsertionTime;
            UpdateInsertingAppearance(uid, true, matProto?.Color);
        }

        /// <summary>
        /// Sets the machine sprite to either play the running animation
        /// or stop.
        /// </summary>
        private void UpdateRunningAppearance(EntityUid uid, bool isRunning)
        {
            _appearance.SetData(uid, LatheVisuals.IsRunning, isRunning);
        }

        /// <summary>
        /// Sets the machine sprite to play the inserting animation
        /// and sets the color of the inserted mat if applicable
        /// </summary>
        private void UpdateInsertingAppearance(EntityUid uid, bool isInserting, Color? color = null)
        {
            _appearance.SetData(uid, LatheVisuals.IsInserting, isInserting);
            if (color != null)
                _appearance.SetData(uid, LatheVisuals.InsertingColor, color);
        }

        private void OnPowerChanged(EntityUid uid, LatheComponent component, PowerChangedEvent args)
        {
            if (!args.Powered)
            {
                if (TryComp<LatheProducingComponent>(uid, out var prodcomp))
                    RemComp(uid, prodcomp);
            }
            else if (component.Recipe != null)
            {
                EnsureComp<LatheProducingComponent>(uid);
            }
            UpdateRunningAppearance(uid, args.Powered && HasComp<LatheProducingComponent>(uid));
        }

        protected override bool HasRecipe(EntityUid uid, LatheRecipePrototype recipe, LatheComponent component)
        {
            return GetAvailableRecipes(component).Contains(recipe.ID);
        }

        #region UI Messages

        private void OnLatheQueueRecipeMessage(EntityUid uid, LatheComponent component, LatheQueueRecipeMessage args)
        {
            if (_proto.TryIndex(args.ID, out LatheRecipePrototype? recipe))
            {
                for (var i = 0; i < args.Quantity; i++)
                {
                    TryAddToQueue(uid, recipe, component);
                }
            }
            TryStartProducing(uid, component);
            UpdateUserInterfaceState(uid, component);
        }

        private void OnLatheSyncRequestMessage(EntityUid uid, LatheComponent component, LatheSyncRequestMessage args)
        {
            UpdateUserInterfaceState(uid, component);
        }

        private void OnLatheServerSelectionMessage(EntityUid uid, LatheComponent component, LatheServerSelectionMessage args)
        {
            // TODO: one day, when you can open BUIs clientside, do that. Until then, picture Electro seething.
            if (component.DynamicRecipes != null)
                _uiSys.TryOpen(uid, ResearchClientUiKey.Key, (IPlayerSession) args.Session);
        }

        private void OnLatheServerSyncMessage(EntityUid uid, TechnologyDatabaseComponent component, LatheServerSyncMessage args)
        {
            Logger.Debug("OnLatheServerSyncMessage");
            _researchSys.SyncWithServer(component);
            UpdateUserInterfaceState(uid);
        }

        #endregion
    }
}
