using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.Cargo.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class CargoConsoleMenu : FancyWindow
    {
        private IEntityManager _entityManager;
        private IPrototypeManager _protoManager;
        private SpriteSystem _spriteSystem;
        private EntityUid _owner;

        public event Action<ButtonEventArgs>? OnItemSelected;
        public event Action<ButtonEventArgs>? OnOrderApproved;
        public event Action<ButtonEventArgs>? OnOrderCanceled;
        public event Action<ButtonEventArgs>? OnOrderRestricted;

        private readonly List<string> _categoryStrings = new();
        private string? _category;
        private List<CargoRestrictedData> _restrictedData = new();

        public CargoConsoleMenu(EntityUid owner, IEntityManager entMan, IPrototypeManager protoManager, SpriteSystem spriteSystem)
        {
            RobustXamlLoader.Load(this);
            _entityManager = entMan;
            _protoManager = protoManager;
            _spriteSystem = spriteSystem;
            _owner = owner;

            Title = Loc.GetString("cargo-console-menu-title");

            SearchBar.OnTextChanged += OnSearchBarTextChanged;
            Categories.OnItemSelected += OnCategoryItemSelected;
        }

        private void OnCategoryItemSelected(OptionButton.ItemSelectedEventArgs args)
        {
            SetCategoryText(args.Id);
            PopulateProducts();
        }

        private void OnSearchBarTextChanged(LineEdit.LineEditEventArgs args)
        {
            PopulateProducts();
        }

        private void SetCategoryText(int id)
        {
            _category = id == 0 ? null : _categoryStrings[id];
            Categories.SelectId(id);
        }

        public IEnumerable<CargoProductPrototype> ProductPrototypes
        {
            get
            {
                var allowedGroups = _entityManager.GetComponentOrNull<CargoOrderConsoleComponent>(_owner)?.AllowedGroups;

                foreach (var cargoPrototype in _protoManager.EnumeratePrototypes<CargoProductPrototype>())
                {
                    if (!allowedGroups?.Contains(cargoPrototype.Group) ?? false)
                        continue;

                    yield return cargoPrototype;
                }
            }
        }

        /// <summary>
        ///     Populates the list of products that will actually be shown, using the current filters.
        /// </summary>
        public void PopulateProducts()
        {
            Products.RemoveAllChildren();
            var products = ProductPrototypes.ToList();
            products.Sort((x, y) =>
                string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase));

            var search = SearchBar.Text.Trim().ToLowerInvariant();

            var restrictedIds = GetRestrictedIds();
            foreach (var prototype in products)
            {
                // if no search or category
                // else if search
                // else if category and not search
                if (search.Length == 0 && _category == null ||
                    search.Length != 0 && prototype.Name.ToLowerInvariant().Contains(search) ||
                    search.Length != 0 && prototype.Description.ToLowerInvariant().Contains(search) ||
                    search.Length == 0 && _category != null && Loc.GetString(prototype.Category).Equals(_category))
                {

                    var button = new CargoProductRow
                    {
                        Product = prototype,
                        ProductName = { Text = prototype.Name },
                        MainButton = { ToolTip = prototype.Description },
                        PointCost = { Text = Loc.GetString("cargo-console-menu-points-amount", ("amount", prototype.Cost.ToString())) },
                        Icon = { Texture = _spriteSystem.Frame0(prototype.Icon) },
                        Restricted = { Pressed = restrictedIds.Contains(prototype.ID) },
                    };
                    button.MainButton.OnPressed += args =>
                    {
                        OnItemSelected?.Invoke(args);
                    };
                    button.Restricted.OnPressed += args =>
                    {
                        OnOrderRestricted?.Invoke(args);
                    };
                    Products.AddChild(button);
                }
            }
        }

        /// <summary>
        ///     Populates the list of products that will actually be shown, using the current filters.
        /// </summary>
        public void PopulateCategories()
        {
            _categoryStrings.Clear();
            Categories.Clear();

            foreach (var prototype in ProductPrototypes)
            {
                if (!_categoryStrings.Contains(Loc.GetString(prototype.Category)))
                {
                    _categoryStrings.Add(Loc.GetString(prototype.Category));
                }
            }

            _categoryStrings.Sort();

            // Add "All" category at the top of the list
            _categoryStrings.Insert(0, Loc.GetString("cargo-console-menu-populate-categories-all-text"));

            foreach (var str in _categoryStrings)
            {
                Categories.AddItem(str);
            }
        }

        /// <summary>
        ///     Populates the list of orders and requests.
        /// </summary>
        public void PopulateOrders(IEnumerable<CargoOrderData> orders)
        {
            Orders.DisposeAllChildren();
            Requests.DisposeAllChildren();

            foreach (var order in orders)
            {
                var product = _protoManager.Index<EntityPrototype>(order.ProductId);
                var productName = product.Name;

                var row = new CargoOrderRow
                {
                    Order = order,
                    Icon = { Texture = _spriteSystem.Frame0(product) },
                    ProductName =
                    {
                        Text = Loc.GetString(
                            "cargo-console-menu-populate-orders-cargo-order-row-product-name-text",
                            ("productName", productName),
                            ("orderAmount", order.OrderQuantity),
                            ("orderRequester", order.Requester))
                    },
                    Description = {Text = Loc.GetString("cargo-console-menu-order-reason-description",
                                                        ("reason", order.Reason))}
                };
                row.Cancel.OnPressed += (args) => { OnOrderCanceled?.Invoke(args); };
                if (order.Approved)
                {
                    row.Approve.Visible = false;
                    row.Cancel.Visible = false;
                    Orders.AddChild(row);
                }
                else
                {
                    // TODO: Disable based on access.
                    row.Approve.OnPressed += (args) => { OnOrderApproved?.Invoke(args); };
                    Requests.AddChild(row);
                }
            }
        }

        public void UpdateCargoCapacity(int count, int capacity)
        {
            // TODO: Rename + Loc.
            ShuttleCapacityLabel.Text = $"{count}/{capacity}";
        }

        public void UpdateBankData(string name, int points)
        {
            AccountNameLabel.Text = name;
            PointsLabel.Text = Loc.GetString("cargo-console-menu-points-amount", ("amount", points.ToString()));
        }

        public void UpdateRestrictedData(IEnumerable<CargoRestrictedData> restrictedData)
        {
            _restrictedData = restrictedData.ToList();
        }

        public List<string> GetRestrictedIds()
        {
            List<string> restrictedIds = new();
            foreach (var restrictedData in _restrictedData)
            {
                restrictedIds.Add(restrictedData.ProductId);
            }
            return restrictedIds;
        }
    }
}
