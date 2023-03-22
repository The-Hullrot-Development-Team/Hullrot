using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.Construction;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping.Unary.Visuals;
using Content.Shared.Atmos.Piping.Unary.Components;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Examine;

namespace Content.Server.Atmos.Piping.Unary.EntitySystems
{
    [UsedImplicitly]
    public sealed class GasThermoMachineSystem : EntitySystem
    {
        [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly PowerReceiverSystem _power = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<GasThermoMachineComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<GasThermoMachineComponent, AtmosDeviceUpdateEvent>(OnThermoMachineUpdated);
            SubscribeLocalEvent<GasThermoMachineComponent, AtmosDeviceDisabledEvent>(OnThermoMachineLeaveAtmosphere);
            SubscribeLocalEvent<GasThermoMachineComponent, RefreshPartsEvent>(OnGasThermoRefreshParts);
            SubscribeLocalEvent<GasThermoMachineComponent, UpgradeExamineEvent>(OnGasThermoUpgradeExamine);
            SubscribeLocalEvent<GasThermoMachineComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<GasThermoMachineComponent, ExaminedEvent>(OnExamined);

            // UI events
            SubscribeLocalEvent<GasThermoMachineComponent, GasThermomachineToggleMessage>(OnToggleMessage);
            SubscribeLocalEvent<GasThermoMachineComponent, GasThermomachineChangeTemperatureMessage>(OnChangeTemperature);
        }

        private void OnInit(EntityUid uid, GasThermoMachineComponent thermoMachine, ComponentInit args)
        {
            if (TryComp<ApcPowerReceiverComponent>(uid, out var apcPowerReceiver))
            {
                thermoMachine.ActivePower = apcPowerReceiver.Load; //inherit powerLoad prototype variable from ApcPowerReceiverComponent
            }

            UpdateState(uid, thermoMachine);
        }

        private void OnPowerChanged(EntityUid uid, GasThermoMachineComponent thermoMachine, ref PowerChangedEvent args)
        {
            UpdateState(uid, thermoMachine);
        }

        private void OnThermoMachineUpdated(EntityUid uid, GasThermoMachineComponent thermoMachine, AtmosDeviceUpdateEvent args)
        {
            if (!thermoMachine.IsRunning
                || !TryComp(uid, out NodeContainerComponent? nodeContainer)
                || !nodeContainer.TryGetNode(thermoMachine.InletName, out PipeNode? inlet))
            {
                return;
            }

            var airHeatCapacity = _atmosphereSystem.GetHeatCapacity(inlet.Air);
            var combinedHeatCapacity = airHeatCapacity + thermoMachine.HeatCapacity;

            if (!MathHelper.CloseTo(combinedHeatCapacity, 0, 0.001f))
            {
                var combinedEnergy = thermoMachine.HeatCapacity * thermoMachine.TargetTemperature + airHeatCapacity * inlet.Air.Temperature;
                inlet.Air.Temperature = combinedEnergy / combinedHeatCapacity;
            }
        }

        private void OnThermoMachineLeaveAtmosphere(EntityUid uid, GasThermoMachineComponent thermoMachine, AtmosDeviceDisabledEvent args)
        {
            UpdateState(uid, thermoMachine);
        }


        private void OnGasThermoRefreshParts(EntityUid uid, GasThermoMachineComponent thermoMachine, RefreshPartsEvent args)
        {
            var matterBinRating = args.PartRatings[thermoMachine.MachinePartHeatCapacity];
            var laserRating = args.PartRatings[thermoMachine.MachinePartTemperature];

            thermoMachine.HeatCapacity = thermoMachine.BaseHeatCapacity * MathF.Pow(matterBinRating, 2);

            switch (thermoMachine.Mode)
            {
                // 593.15K with stock parts.
                case ThermoMachineMode.Heater:
                    thermoMachine.MaxTemperature = thermoMachine.BaseMaxTemperature + thermoMachine.MaxTemperatureDelta * laserRating;
                    thermoMachine.MinTemperature = Atmospherics.T20C;
                    break;
                // 73.15K with stock parts.
                case ThermoMachineMode.Freezer:
                    thermoMachine.MinTemperature = MathF.Max(
                        thermoMachine.BaseMinTemperature - thermoMachine.MinTemperatureDelta * laserRating, Atmospherics.TCMB);
                    thermoMachine.MaxTemperature = Atmospherics.T20C;
                    break;
            }

            DirtyUI(uid, thermoMachine);
        }

        private void OnGasThermoUpgradeExamine(EntityUid uid, GasThermoMachineComponent thermoMachine, UpgradeExamineEvent args)
        {
            switch (thermoMachine.Mode)
            {
                case ThermoMachineMode.Heater:
                    args.AddPercentageUpgrade("gas-thermo-component-upgrade-heating", thermoMachine.MaxTemperature / (thermoMachine.BaseMaxTemperature + thermoMachine.MaxTemperatureDelta));
                    break;
                case ThermoMachineMode.Freezer:
                    args.AddPercentageUpgrade("gas-thermo-component-upgrade-cooling", thermoMachine.MinTemperature / (thermoMachine.BaseMinTemperature - thermoMachine.MinTemperatureDelta));
                    break;
            }
            args.AddPercentageUpgrade("gas-thermo-component-upgrade-heat-capacity", thermoMachine.HeatCapacity / thermoMachine.BaseHeatCapacity);
        }

        private void OnToggleMessage(EntityUid uid, GasThermoMachineComponent thermoMachine, GasThermomachineToggleMessage args)
        {
            SetEnabled(uid, thermoMachine, !thermoMachine.Enabled);
            UpdateState(uid, thermoMachine);
            DirtyUI(uid, thermoMachine);
        }

        private void OnChangeTemperature(EntityUid uid, GasThermoMachineComponent thermoMachine, GasThermomachineChangeTemperatureMessage args)
        {
            thermoMachine.TargetTemperature =
                Math.Clamp(args.Temperature, thermoMachine.MinTemperature, thermoMachine.MaxTemperature);

            DirtyUI(uid, thermoMachine);
        }

        private void DirtyUI(EntityUid uid, GasThermoMachineComponent? thermoMachine, ServerUserInterfaceComponent? ui=null)
        {
            if (!Resolve(uid, ref thermoMachine, ref ui, false))
                return;

            _userInterfaceSystem.TrySetUiState(uid, ThermomachineUiKey.Key,
                new GasThermomachineBoundUserInterfaceState(thermoMachine.MinTemperature, thermoMachine.MaxTemperature, thermoMachine.TargetTemperature, thermoMachine.Enabled, thermoMachine.Mode), null, ui);
        }

        /// <summary>
        ///      Updates the running state of the machine, adjust the power consumption accordingly and updates its appearance.
        /// </summary>
        private void UpdateState(EntityUid uid, GasThermoMachineComponent? thermoMachine = null, AppearanceComponent? appearance = null)
        {
            if (!Resolve(uid, ref thermoMachine, ref appearance, false))
                return;

            thermoMachine.IsRunning = thermoMachine.Enabled && _power.IsPowered(uid);
            if (TryComp<ApcPowerReceiverComponent>(uid, out var apcPowerReceiver))
            {
                apcPowerReceiver.Load = thermoMachine.Enabled ? thermoMachine.ActivePower : thermoMachine.IdlePower;
            }

            _appearance.SetData(uid, ThermoMachineVisuals.Running, thermoMachine.IsRunning, appearance);
        }

        private void SetEnabled(EntityUid uid, GasThermoMachineComponent thermoMachine, bool enabled)
        {
            thermoMachine.Enabled = enabled;
        }

        private void OnExamined(EntityUid uid, GasThermoMachineComponent thermoMachine, ExaminedEvent args)
        {
            if (!args.IsInDetailsRange)
                return;

            if (Loc.TryGetString("gas-thermomachine-system-examined", out var str,
                        ("machineName", thermoMachine.Mode == ThermoMachineMode.Freezer ? "freezer" : "heater"),
                        ("tempColor", thermoMachine.Mode == ThermoMachineMode.Freezer ? "blue" : "red"),
                        ("temp", Math.Round(thermoMachine.TargetTemperature,2))
            ))
                args.PushMarkup(str);
        }

    }
}
