using System.Linq;
using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.Crayon;
using Content.Shared.Decals;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.Crayon.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class CrayonWindow : DefaultWindow
    {
        [Dependency] private readonly IEntitySystemManager _entitySystem = default!;
        private readonly SpriteSystem _spriteSystem = default!;

        private Dictionary<string, List<(string Name, Texture Texture)>>? _decals;
        private List<string>? _allDecals;
        private string? _autoSelected;
        private string? _selected;
        private Color _color;

        public event Action<Color>? OnColorSelected;
        public event Action<string>? OnSelected;

        public CrayonWindow()
        {
            RobustXamlLoader.Load(this);
            IoCManager.InjectDependencies(this);
            _spriteSystem = _entitySystem.GetEntitySystem<SpriteSystem>();

            Search.OnTextChanged += SearchChanged;
            ColorSelector.OnColorChanged += SelectColor;
        }

        private void SelectColor(Color color)
        {
            _color = color;

            OnColorSelected?.Invoke(color);
            RefreshList();
        }

        private void RefreshList()
        {
            // Clear
            Grids.DisposeAllChildren();

            if (_decals == null || _allDecals == null)
                return;

            var filter = Search.Text;
            var comma = filter.IndexOf(',');
            var first = (comma == -1 ? filter : filter[..comma]).Trim();

            var names = _decals.Keys.ToList();
            names.Sort((a, b) => a == "random" ? 1 : b == "random" ? -1 : a.CompareTo(b));

            if (_autoSelected != null && first != _autoSelected && _allDecals.Contains(first))
            {
                _selected = first;
                _autoSelected = _selected;
                OnSelected?.Invoke(_selected);
            }

            foreach (var categoryName in names)
            {
                var locName = Loc.GetString("crayon-category-" + categoryName);
                var category = _decals[categoryName].Where(d => locName.Contains(first) || d.Name.Contains(first)).ToList();

                if (category.Count == 0)
                    continue;

                var label = new Label
                {
                    Text = locName
                };

                var grid = new GridContainer
                {
                    Columns = 6,
                    Margin = new Thickness(0, 0, 0, 16)
                };

                Grids.AddChild(label);
                Grids.AddChild(grid);

                foreach (var (name, texture) in category)
                {
                    var button = new TextureButton()
                    {
                        TextureNormal = texture,
                        Name = name,
                        ToolTip = name,
                        Modulate = _color,
                        Scale = new Vector2(2, 2)
                    };
                    button.OnPressed += ButtonOnPressed;

                    if (_selected == name)
                    {
                        var panelContainer = new PanelContainer()
                        {
                            PanelOverride = new StyleBoxFlat()
                            {
                                BackgroundColor = StyleNano.ButtonColorDefault,
                            },
                            Children =
                            {
                                button,
                            },
                        };
                        grid.AddChild(panelContainer);
                    }
                    else
                    {
                        grid.AddChild(button);
                    }
                }
            }
        }

        private void SearchChanged(LineEdit.LineEditEventArgs obj)
        {
            _autoSelected = ""; // Placeholder to kick off the auto-select in refreshlist()
            RefreshList();
        }

        private void ButtonOnPressed(ButtonEventArgs obj)
        {
            if (obj.Button.Name == null) return;

            _selected = obj.Button.Name;
            _autoSelected = null;
            OnSelected?.Invoke(_selected);
            RefreshList();
        }

        public void UpdateState(CrayonBoundUserInterfaceState state)
        {
            _selected = state.Selected;
            ColorSelector.Visible = state.SelectableColor;
            _color = state.Color;

            if (ColorSelector.Visible)
            {
                ColorSelector.Color = state.Color;
            }

            RefreshList();
        }

        public void AdvanceState(string drawnDecal)
        {
            var filter = Search.Text;
            if (!filter.Contains(',') || !filter.Contains(drawnDecal))
                return;

            var first = filter[..filter.IndexOf(',')].Trim();

            if (first.Equals(drawnDecal, StringComparison.InvariantCultureIgnoreCase))
            {
                Search.Text = filter[(filter.IndexOf(',') + 1)..].Trim();
                _autoSelected = first;
            }

            RefreshList();
        }

        public void Populate(List<DecalPrototype> prototypes)
        {
            _decals = [];
            _allDecals = [];

            prototypes.Sort((a, b) => a.ID.CompareTo(b.ID));

            foreach (var decalPrototype in prototypes)
            {
                var category = "random";
                if (decalPrototype.Tags.Count > 1 && decalPrototype.Tags[1].StartsWith("crayon-"))
                    category = decalPrototype.Tags[1].Replace("crayon-", "");
                var list = _decals.GetOrNew(category);
                list.Add((decalPrototype.ID, _spriteSystem.Frame0(decalPrototype.Sprite)));
                _allDecals.Add(decalPrototype.ID);
            }

            RefreshList();
        }
    }
}
