using System.Diagnostics.CodeAnalysis;
using Content.Server.Chat.Systems;
using Content.Server.DeviceNetwork.Components;
using Content.Server.Fax;
using Content.Server.Paper;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;

namespace Content.Server.Nuke
{
    public sealed class NukeCodePaperSystem : EntitySystem
    {
        [Dependency] private readonly ChatSystem _chatSystem = default!;
        [Dependency] private readonly StationSystem _station = default!;
        [Dependency] private readonly PaperSystem _paper = default!;
        [Dependency] private readonly FaxSystem _faxSystem = default!;
        
        private const string CentcomFaxPrototypeId = "FaxMachineCentcom";

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<NukeCodePaperComponent, MapInitEvent>(OnMapInit,
                after: new []{ typeof(NukeLabelSystem) });
        }

        private void OnMapInit(EntityUid uid, NukeCodePaperComponent component, MapInitEvent args)
        {
            SetupPaper(uid);
        }

        private void SetupPaper(EntityUid uid, EntityUid? station = null)
        {
            if (TryGetRelativeNukeCode(uid, out var paperContent, station))
            {
                _paper.SetContent(uid, paperContent);
            }
        }

        /// <summary>
        ///     Send a nuclear code to all faxes on that station which are authorized to receive nuke codes.
        /// </summary>
        /// <returns>True if at least one fax received codes</returns>
        public bool SendNukeCodes(EntityUid station)
        {
            if (!HasComp<StationDataComponent>(station))
            {
                return false;
            }

            var faxes = EntityManager.EntityQuery<FaxMachineComponent>();

            string? centcomFaxAddress = null;
            foreach (var fax in faxes)
            {
                if (!TryComp<MetaDataComponent>(fax.Owner, out var metadata) ||
                    metadata.EntityPrototype?.ID != CentcomFaxPrototypeId ||
                    !TryComp<DeviceNetworkComponent>(fax.Owner, out var device))
                {
                    continue;
                }

                centcomFaxAddress = device.Address;
                break;
            }

            var wasSent = false;
            foreach (var fax in faxes)
            {
                if (!fax.ReceiveNukeCodes || !TryGetRelativeNukeCode(fax.Owner, out var paperContent, station))
                {
                    continue;
                }

                var printout = new FaxPrintout(
                    paperContent,
                    Loc.GetString("nuke-codes-fax-paper-name"),
                    "paper_stamp-cent",
                    new() { Loc.GetString("stamp-component-stamped-name-centcom") });
                _faxSystem.Receive(fax.Owner, printout, centcomFaxAddress, fax);

                wasSent = true;
            }

            if (wasSent)
            {
                var msg = Loc.GetString("nuke-component-announcement-send-codes");
                _chatSystem.DispatchStationAnnouncement(station, msg, colorOverride: Color.Red);
            }

            return wasSent;
        }

        private bool TryGetRelativeNukeCode(
            EntityUid uid,
            [NotNullWhen(true)] out string? nukeCode,
            EntityUid? station = null,
            TransformComponent? transform = null)
        {
            nukeCode = null;
            if (!Resolve(uid, ref transform))
            {
                return false;
            }
            
            var owningStation = station ?? _station.GetOwningStation(uid);
            
            // Find the first nuke that matches the passed location.
            foreach (var nuke in EntityQuery<NukeComponent>())
            {
                if (owningStation == null && nuke.OriginMapGrid != (transform.MapID, transform.GridUid)
                    || nuke.OriginStation != owningStation)
                {
                    continue;
                }

                nukeCode = Loc.GetString("nuke-codes-message", ("name", MetaData(nuke.Owner).EntityName), ("code", nuke.Code));
                return true;
            }

            return false;
        }
    }
}
