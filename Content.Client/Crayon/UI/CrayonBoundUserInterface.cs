using System.Linq;
using Content.Shared.Crayon;
using Content.Shared.Decals;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.Crayon.UI
{
    public sealed class CrayonBoundUserInterface : BoundUserInterface
    {
        [Dependency] private readonly IPrototypeManager _protoManager = default!;

        [ViewVariables]
        private CrayonWindow? _menu;

        public CrayonBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();
            _menu = this.CreateWindow<CrayonWindow>();
            _menu.OnColorSelected += SelectColor;
            _menu.OnSelected += Select;
            _menu.OnRotationSelected += SelectRotation;
            _menu.OnPreviewModeToggled += TogglePreviewMode;
            PopulateCrayons();
            _menu.OpenCenteredLeft();
        }

        private void PopulateCrayons()
        {
            var crayonDecals = _protoManager.EnumeratePrototypes<DecalPrototype>().Where(x => x.Tags.Contains("crayon"));
            _menu?.Populate(crayonDecals.ToList());
        }

        public override void OnProtoReload(PrototypesReloadedEventArgs args)
        {
            base.OnProtoReload(args);

            if (!args.WasModified<DecalPrototype>())
                return;

            PopulateCrayons();
        }

        protected override void ReceiveMessage(BoundUserInterfaceMessage message)
        {
            base.ReceiveMessage(message);

            if (_menu is null || message is not CrayonUsedMessage crayonMessage)
                return;

            _menu.AdvanceState(crayonMessage.DrawnDecal);
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            _menu?.UpdateState((CrayonBoundUserInterfaceState) state);
        }

        public void Select(string state)
        {
            SendPredictedMessage(new CrayonSelectMessage(state));
        }

        public void SelectColor(Color color)
        {
            SendPredictedMessage(new CrayonColorMessage(color));
        }

        public void SelectRotation(float rotation)
        {
            SendPredictedMessage(new CrayonRotationMessage(rotation));
        }

        public void TogglePreviewMode(bool previewMode)
        {
            SendMessage(new CrayonPreviewModeMessage(previewMode));
        }
    }
}
