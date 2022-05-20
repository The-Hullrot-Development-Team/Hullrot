using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping.Unary.Components;

namespace Content.Server.Atmos.Piping.Unary.Components
{
    [RegisterComponent]
    public sealed class GasThermoMachineComponent : Component
    {
        [DataField("inlet")]
        public string InletName { get; set; } = "pipe";

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        ///     Current maximum temperature, calculated from <see cref="BaseHeatCapacity"/> and the quality of matter
        ///     bins. The heat capacity effectively determines the rate at which the thermo machine can add or remove
        ///     heat from a pipenet.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public float HeatCapacity = 10000;

        /// <summary>
        ///     Base heat capacity of the device. Actual heat capacity is calculated by taking this number and doubling
        ///     it for every matter bin quality tier above one.
        /// </summary>
        [DataField("baseHeatCapacity ")]
        public float BaseHeatCapacity = 5000;

        [DataField("targetTemperature")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float TargetTemperature { get; set; } = Atmospherics.T20C;

        [DataField("mode")]
        public ThermoMachineMode Mode { get; set; } = ThermoMachineMode.Freezer;

        /// <summary>
        ///     Current minimum temperature, calculated from <see cref="InitialMinTemperature"/> and <see
        ///     cref="MinTemperatureDelta"/>.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public float MinTemperature = 88.15f;

        /// <summary>
        ///     Current maximum temperature, calculated from <see cref="InitialMaxTemperature"/> and <see
        ///     cref="MaxTemperatureDelta"/>.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public float MaxTemperature = Atmospherics.T20C;

        /// <summary>
        ///     Minimum temperature the device can reach with a 0 total laser quality. Usually the quality will be at
        ///     least 1.
        /// </summary>
        [DataField("baseMinTemperature")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float BaseMinTemperature = 259.85f;

        /// <summary>
        ///     Maximum temperature the device can reach with a 0 total laser quality. Usually the quality will be at
        ///     least 1.
        /// </summary>
        [DataField("baseMaxTemperature")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float BaseMaxTemperature = Atmospherics.T20C;

        /// <summary>
        ///     Decrease in minimum temperature, per unit machine part quality.
        /// </summary>
        [DataField("minTemperatureDelta")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float MinTemperatureDelta = 66.7f;

        /// <summary>
        ///     Change in maximum temperature, per unit machine part quality.
        /// </summary>
        [DataField("maxTemperatureDelta")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float MaxTemperatureDelta = 3 * Atmospherics.T20C;
    }
}
