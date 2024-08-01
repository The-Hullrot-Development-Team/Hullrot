using System.Collections.Generic;
using Content.Client.Decals;
using Content.Client.Stylesheets;
using Content.Shared.Crayon;
using Content.Shared.Decals;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Client.Utility;
using Robust.Shared.Graphics;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.Crayon.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class CrayonWindow : DefaultWindow
    {
        [Dependency] private readonly IEntityManager _e = default!;

        private readonly CrayonSystem _crayonSystem;

        private Dictionary<string, Texture>? _decals;
        private string? _selected;
        private Color _color;
        private float _rotation;

        public FloatSpinBox RotationSpinBox;

        public event Action<Color>? OnColorSelected;
        public event Action<string>? OnSelected;

        public CrayonWindow()
        {
            RobustXamlLoader.Load(this);
            IoCManager.InjectDependencies(this);

            _crayonSystem = _e.System<CrayonSystem>();

            Search.OnTextChanged += _ => RefreshList();
            ColorSelector.OnColorChanged += SelectColor;

            RotationSpinBox = new FloatSpinBox(90.0f, 0)
            {
                Value = _rotation,
                HorizontalExpand = true
            };
            SpinBoxContainer.AddChild(RotationSpinBox);

            RotationSpinBox.OnValueChanged += args =>
            {
                _rotation = args.Value;
                Owner.SelectRotation(_rotation);
                UpdateCrayonDecalPlacementInfo();
            };
        }

        private void SelectColor(Color color)
        {
            _color = color;

            OnColorSelected?.Invoke(color);
            RefreshList();
            UpdateCrayonDecalPlacementInfo();
        }

        private void RefreshList()
        {
            // Clear
            Grid.DisposeAllChildren();
            if (_decals == null)
                return;

            var filter = Search.Text;
            foreach (var (decal, tex) in _decals)
            {
                if (!decal.Contains(filter))
                    continue;

                var button = new TextureButton()
                {
                    TextureNormal = tex,
                    Name = decal,
                    ToolTip = decal,
                    Modulate = _color,
                };
                button.OnPressed += ButtonOnPressed;
                if (_selected == decal)
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
                    Grid.AddChild(panelContainer);
                }
                else
                {
                    Grid.AddChild(button);
                }
            }
        }

        private void ButtonOnPressed(ButtonEventArgs obj)
        {
            if (obj.Button.Name == null) return;

            _selected = obj.Button.Name;
            OnSelected?.Invoke(_selected);
            RefreshList();
            UpdateCrayonDecalPlacementInfo();
        }

        public void UpdateState(CrayonBoundUserInterfaceState state)
        {
            _selected = state.Selected;
            ColorSelector.Visible = state.SelectableColor;
            _color = state.Color;
            _rotation = state.Rotation;

            if (ColorSelector.Visible)
            {
                ColorSelector.Color = state.Color;
            }

            RotationSpinBox.Value = state.Rotation;

            RefreshList();
            UpdateCrayonDecalPlacementInfo();
        }

        public void Populate(IEnumerable<DecalPrototype> prototypes)
        {
            _decals = new Dictionary<string, Texture>();
            foreach (var decalPrototype in prototypes)
            {
                _decals.Add(decalPrototype.ID, decalPrototype.Sprite.Frame0());
            }

            RefreshList();
        }

        private void UpdateCrayonDecalPlacementInfo()
        {
            if (_selected is null)
                return;

            _crayonSystem.UpdateCrayonDecalInfo(_selected, _color, _rotation);
        }

        protected override void Opened()
        {
            base.Opened();
            _crayonSystem.SetActive(true);
        }

        public override void Close()
        {
            base.Close();
            _crayonSystem.SetActive(false);
        }
    }
}
