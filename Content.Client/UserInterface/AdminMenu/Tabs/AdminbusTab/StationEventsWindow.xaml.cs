#nullable enable
using System.Linq;
using System.Collections.Generic;
using Content.Client.StationEvents;
using JetBrains.Annotations;
using Robust.Client.AutoGenerated;
using Robust.Client.Console;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;

namespace Content.Client.UserInterface.AdminMenu.Tabs.AdminbusTab
{
    [GenerateTypedNameReferences]
    [UsedImplicitly]
    public partial class StationEventsWindow : SS14Window
    {
        private List<string>? _data;

        protected override void EnteredTree()
        {
            _data = IoCManager.Resolve<IStationEventManager>().StationEvents.ToList();
            _data.Add(_data.Any() ? Loc.GetString("station-events-window-not-loaded-text") : Loc.GetString("generic-random"));
            foreach (var stationEvent in _data)
            {
                EventsOptions.AddItem(stationEvent);
            }

            EventsOptions.OnItemSelected += eventArgs => EventsOptions.SelectId(eventArgs.Id);
            PauseButton.OnPressed += PauseButtonOnOnPressed;
            ResumeButton.OnPressed += ResumeButtonOnOnPressed;
            SubmitButton.OnPressed += SubmitButtonOnOnPressed;
        }

        private static void PauseButtonOnOnPressed(BaseButton.ButtonEventArgs obj)
        {
            IoCManager.Resolve<IClientConsoleHost>().ExecuteCommand("events pause");
        }

        private static void ResumeButtonOnOnPressed(BaseButton.ButtonEventArgs obj)
        {
            IoCManager.Resolve<IClientConsoleHost>().ExecuteCommand("events resume");
        }

        private void SubmitButtonOnOnPressed(BaseButton.ButtonEventArgs obj)
        {
            if (_data == null)
                return;
            var selectedEvent = _data[EventsOptions.SelectedId];
            IoCManager.Resolve<IClientConsoleHost>().ExecuteCommand($"events run \"{selectedEvent}\"");
        }
    }
}
