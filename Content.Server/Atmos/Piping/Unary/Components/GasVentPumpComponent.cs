using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.DeviceLinking;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Atmos.Piping.Unary.Components
{
    // The world if people documented their shit.
    [AutoGenerateComponentPause]
    [RegisterComponent]
    public sealed partial class GasVentPumpComponent : Component
    {
        [ViewVariables(VVAccess.ReadWrite)]
        public bool Enabled { get; set; } = false;

        [ViewVariables]
        public bool IsDirty { get; set; } = false;

        [DataField]
        public string Inlet { get; set; } = "pipe";

        [DataField]
        public string Outlet { get; set; } = "pipe";

        [DataField]
        public VentPumpDirection PumpDirection { get; set; } = VentPumpDirection.Releasing;

        [DataField]
        public VentPressureBound PressureChecks { get; set; } = VentPressureBound.ExternalBound;

        [DataField]
        public bool UnderPressureLockout { get; set; } = false;

        /// <summary>
        ///     In releasing mode, do not pump when environment pressure is below this limit.
        /// </summary>
        [DataField]
        public float UnderPressureLockoutThreshold = 80; // this must be tuned in conjunction with atmos.mmos_spacing_speed

        /// <summary>
        ///     Pressure locked vents still leak a little (leading to eventual pressurization of sealed sections)
        /// </summary>
        /// <remarks>
        ///     Ratio of pressure difference between pipes and atmosphere that will leak each second, in moles.
        ///     If the pipes are 200 kPa and the room is spaced, at 0.01 UnderPressureLockoutLeaking, the room will fill
        ///     at a rate of 2 moles / sec. It will then reach 2 kPa (UnderPressureLockoutThreshold) and begin normal
        ///     filling after about 20 seconds (depending on room size).
        ///
        ///     Since we want to prevent automating the work of atmos, the leaking rate of 0.0001f is set to make auto
        ///     repressurizing of the development map take about 30 minutes using an oxygen tank (high pressure)
        /// </remarks>

        [DataField]
        public float UnderPressureLockoutLeaking = 0.0001f;
        /// <summary>
        /// Is the vent pressure lockout currently manually disabled?
        /// </summary>
        [DataField]
        public bool IsPressureLockoutManuallyDisabled = false;
        /// <summary>
        /// The time when the manual pressure lockout will be reenabled. 
        /// </summary>
        [DataField]
        [AutoPausedField]
        public TimeSpan ManualLockoutReenabledAt;
        /// <summary>
        /// How long the lockout should remain manually disabled after being interacted with.
        /// </summary>
        [DataField]
        public TimeSpan ManualLockoutDisabledDuration = TimeSpan.FromSeconds(30); // Enough time to fill a 5x5 room
        /// <summary>
        /// How long the doAfter should take when attempting to manually disable the pressure lockout.
        /// </summary>
        public float ManualLockoutDisableDoAfter = 2.0f;

        [DataField]
        public float ExternalPressureBound
        {
            get => _externalPressureBound;
            set
            {
                _externalPressureBound = Math.Clamp(value, 0, MaxPressure);
            }
        }

        private float _externalPressureBound = Atmospherics.OneAtmosphere;

        [DataField]
        public float InternalPressureBound
        {
            get => _internalPressureBound;
            set
            {
                _internalPressureBound = Math.Clamp(value, 0, MaxPressure);
            }
        }

        private float _internalPressureBound = 0;

        /// <summary>
        ///     Max pressure of the target gas (NOT relative to source).
        /// </summary>
        [DataField]
        public float MaxPressure = Atmospherics.MaxOutputPressure;

        /// <summary>
        ///     Pressure pump speed in kPa/s. Determines how much gas is moved.
        /// </summary>
        /// <remarks>
        ///     The pump will attempt to modify the destination's final pressure by this quantity every second. If this
        ///     is too high, and the vent is connected to a large pipe-net, then someone can nearly instantly flood a
        ///     room with gas.
        /// </remarks>
        [DataField]
        public float TargetPressureChange = Atmospherics.OneAtmosphere;

        /// <summary>
        ///     Ratio of max output air pressure and pipe pressure, representing the vent's ability to increase pressure
        /// </summary>
        /// <remarks>
        ///     Vents cannot suck a pipe completely empty, instead pressurizing a section to a max of
        ///     pipe pressure * PumpPower (in kPa). So a 51 kPa pipe is required for 101 kPA sections at PumpPower 2.0
        /// </remarks>
        [DataField]
        public float PumpPower = 2.0f;

        #region Machine Linking
        /// <summary>
        ///     Whether or not machine linking is enabled for this component.
        /// </summary>
        [DataField]
        public bool CanLink = false;

        [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
        public string PressurizePort = "Pressurize";

        [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
        public string DepressurizePort = "Depressurize";

        [DataField]
        public float PressurizePressure = Atmospherics.OneAtmosphere;

        [DataField]
        public float DepressurizePressure = 0;

        // When true, ignore under-pressure lockout. Used to re-fill rooms in air alarm "Fill" mode.
        [DataField]
        public bool PressureLockoutOverride = false;
        #endregion

        public GasVentPumpData ToAirAlarmData()
        {
            return new GasVentPumpData
            {
                Enabled = Enabled,
                Dirty = IsDirty,
                PumpDirection = PumpDirection,
                PressureChecks = PressureChecks,
                ExternalPressureBound = ExternalPressureBound,
                InternalPressureBound = InternalPressureBound,
                PressureLockoutOverride = PressureLockoutOverride
            };
        }

        public void FromAirAlarmData(GasVentPumpData data)
        {
            Enabled = data.Enabled;
            IsDirty = data.Dirty;
            PumpDirection = data.PumpDirection;
            PressureChecks = data.PressureChecks;
            ExternalPressureBound = data.ExternalPressureBound;
            InternalPressureBound = data.InternalPressureBound;
            PressureLockoutOverride = data.PressureLockoutOverride;
        }
    }
}
