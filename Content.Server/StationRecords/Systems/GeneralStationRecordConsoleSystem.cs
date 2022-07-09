using Content.Server.Station.Systems;
using Content.Shared.StationRecords;
using Robust.Server.GameObjects;

namespace Content.Server.StationRecords.Systems;

public sealed class GeneralStationRecordConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecordsSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, BoundUIOpenedEvent>(OnBoundUiOpened);
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, SelectGeneralStationRecord>(OnKeySelected);
    }

    private void OnBoundUiOpened(EntityUid uid, GeneralStationRecordConsoleComponent component, BoundUIOpenedEvent ev)
    {
        UpdateUserInterface(uid, component);
    }

    private void OnKeySelected(EntityUid uid, GeneralStationRecordConsoleComponent component,
        SelectGeneralStationRecord msg)
    {
        component.ActiveKey = msg.SelectedKey;
        UpdateUserInterface(uid, component);
    }

    private void UpdateUserInterface(EntityUid uid, GeneralStationRecordConsoleComponent? console = null)
    {
        if (!Resolve(uid, ref console))
        {
            return;
        }

        var owningStation = _stationSystem.GetOwningStation(uid);

        if (owningStation == null || !TryComp<StationRecordsComponent>(uid, out var stationRecordsComponent))
        {
            _userInterface.GetUiOrNull(uid, GeneralStationRecordConsoleKey.Key)?.SetState(new GeneralStationRecordConsoleState(null, null, null));
            return;
        }

        if (console.ActiveKey != null)
        {
            var key = console.ActiveKey;
            if (_stationRecordsSystem.TryGetRecord(owningStation.Value, console.ActiveKey.Value, out GeneralStationRecord? record))
            {
                _userInterface.GetUiOrNull(uid, GeneralStationRecordConsoleKey.Key)?.SetState(new GeneralStationRecordConsoleState(key, record, null));
            }
        }
        else
        {
            var enumerator = _stationRecordsSystem.GetRecordsOfType<GeneralStationRecord>(owningStation.Value);

            if (enumerator == null)
            {
                return;
            }

            var result = new Dictionary<StationRecordKey, string>();
            foreach (var pair in enumerator)
            {
                if (pair == null)
                {
                    return;
                }

                result.Add(pair.Value.Item1, pair.Value.Item2.Name);
            }

            _userInterface.GetUiOrNull(uid, GeneralStationRecordConsoleKey.Key)?.SetState(new GeneralStationRecordConsoleState(null, null, result));
        }
    }
}
