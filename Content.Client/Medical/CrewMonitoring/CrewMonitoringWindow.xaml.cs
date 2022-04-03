using Content.Client.UserInterface.Controls;
using Content.Shared.Medical.SuitSensor;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Medical.CrewMonitoring
{
    [GenerateTypedNameReferences]
    public sealed partial class CrewMonitoringWindow : DefaultWindow
    {
        private List<Control> _rowsContent = new();

        public static int IconSize = 16; // XAML has a `VSeparationOverride` of 20 for each row.

        public CrewMonitoringWindow()
        {
            RobustXamlLoader.Load(this);
        }

        public void ShowSensors(List<SuitSensorStatus> stSensors, Vector2 worldPosition, Angle worldRotation, bool snap, float precision)
        {
            ClearAllSensors();

            // add a row for each sensor
            foreach (var sensor in stSensors)
            {
                // add users name and job
                // format: UserName (Job)
                var nameLabel = new Label()
                {
                    Text = $"{sensor.Name} ({sensor.Job})"
                };
                SensorsTable.AddChild(nameLabel);
                _rowsContent.Add(nameLabel);

                // add users status and damage
                // format: IsAlive (TotalDamage)
                var statusText = Loc.GetString(sensor.IsAlive ?
                    "crew-monitoring-user-interface-alive" :
                    "crew-monitoring-user-interface-dead");
                if (sensor.TotalDamage != null)
                {
                    statusText += $" ({sensor.TotalDamage})";
                }
                var statusLabel = new Label()
                {
                    Text = statusText
                };
                SensorsTable.AddChild(statusLabel);
                _rowsContent.Add(statusLabel);

                // add users positions
                // format: (x, y)
                var box = GetPositionBox(sensor.Coordinates, worldPosition, worldRotation, snap, precision);
                SensorsTable.AddChild(box);
                _rowsContent.Add(box);
            }
        }

        private BoxContainer GetPositionBox(MapCoordinates? coordinates, Vector2 sensorPosition, Angle sensorRotation, bool snap, float precision)
        {
            var box = new BoxContainer() { Orientation = LayoutOrientation.Horizontal };

            if (coordinates == null)
            {
                var dirIcon = new DirectionIcon()
                {
                    SetSize = (IconSize, IconSize),
                    Margin = new(0, 0, 4, 0)
                };
                box.AddChild(dirIcon);
                box.AddChild(new Label() { Text = Loc.GetString("crew-monitoring-user-interface-no-info") });
            }
            else
            {
                // todo: add locations names (kitchen, bridge, etc)
                var pos = (Vector2i) coordinates.Value.Position;
                var relPos = coordinates.Value.Position - sensorPosition;
                var dirIcon = new DirectionIcon(relPos, sensorRotation, snap, minDistance: precision)
                {
                    SetSize = (IconSize, IconSize),
                    Margin = new(0, 0, 4, 0)
                };
                box.AddChild(dirIcon);
                box.AddChild(new Label() { Text = pos.ToString() });
            }

            return box;
        }

        private void ClearAllSensors()
        {
            foreach (var child in _rowsContent)
            {
               SensorsTable.RemoveChild(child);
            }
            _rowsContent.Clear();
        }
    }
}
