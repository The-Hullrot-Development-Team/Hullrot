using System.Numerics;
using Content.Client.Computer;
using Content.Client.UserInterface.Controls;
using Content.Shared.Shuttles.BUIStates;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

[GenerateTypedNameReferences]
public sealed partial class ShuttleConsoleWindow : FancyWindow,
    IComputerWindow<ShuttleConsoleBoundInterfaceState>
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private ShuttleConsoleMode _mode = ShuttleConsoleMode.Nav;

    public event Action<MapCoordinates, Angle>? RequestFTL;
    public event Action<NetEntity, Angle>? RequestBeaconFTL;

    public ShuttleConsoleWindow()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        // Mode switching
        NavModeButton.OnPressed += NavPressed;
        MapModeButton.OnPressed += MapPressed;
        DockModeButton.OnPressed += DockPressed;

        // Modes are exclusive
        var group = new ButtonGroup();

        NavModeButton.Group = group;
        MapModeButton.Group = group;
        DockModeButton.Group = group;

        NavModeButton.Pressed = true;
        SetupMode(_mode);

        MapContainer.RequestFTL += (coords, angle) =>
        {
            RequestFTL?.Invoke(coords, angle);
        };

        MapContainer.RequestBeaconFTL += (ent, angle) =>
        {
            RequestBeaconFTL?.Invoke(ent, angle);
        };
    }

    private void ClearModes(ShuttleConsoleMode mode)
    {
        if (mode != ShuttleConsoleMode.Nav)
        {
            NavModeButton.Pressed = false;
            NavContainer.Visible = false;
        }

        if (mode != ShuttleConsoleMode.Map)
        {
            MapModeButton.Pressed = false;
            MapContainer.Visible = false;
            MapContainer.SetMap(MapId.Nullspace, Vector2.Zero);
        }

        if (mode != ShuttleConsoleMode.Dock)
        {
            DockModeButton.Pressed = false;
            DockContainer.Visible = false;
        }
    }

    private void NavPressed(BaseButton.ButtonEventArgs obj)
    {
        SwitchMode(ShuttleConsoleMode.Nav);
    }

    private void MapPressed(BaseButton.ButtonEventArgs obj)
    {
        SwitchMode(ShuttleConsoleMode.Map);
    }

    private void DockPressed(BaseButton.ButtonEventArgs obj)
    {
        SwitchMode(ShuttleConsoleMode.Dock);
    }

    private void SetupMode(ShuttleConsoleMode mode)
    {
        switch (mode)
        {
            case ShuttleConsoleMode.Nav:
                NavContainer.Visible = true;
                break;
            case ShuttleConsoleMode.Map:
                MapContainer.Visible = true;
                break;
            case ShuttleConsoleMode.Dock:
                DockContainer.Visible = true;
                break;
            default:
                throw new NotImplementedException();
        }
    }

    public void SwitchMode(ShuttleConsoleMode mode)
    {
        if (_mode == mode)
            return;

        _mode = mode;
        ClearModes(mode);
        SetupMode(_mode);
    }

    public enum ShuttleConsoleMode : byte
    {
        Nav,
        Map,
        Dock,
    }

    public void SetMatrix(EntityCoordinates? coordinates, Angle? angle)
    {
        NavContainer.SetMatrix(coordinates, angle);
    }

    public void UpdateState(ShuttleConsoleBoundInterfaceState cState)
    {
        NavContainer.UpdateState(cState);
        var coordinates = _entManager.GetCoordinates(cState.Coordinates);
        NavContainer.SetShuttle(coordinates?.EntityId);
        MapContainer.SetShuttle(coordinates?.EntityId);
        MapContainer.UpdateState(cState);
    }
}
