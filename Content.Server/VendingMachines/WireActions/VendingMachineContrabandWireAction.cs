using Content.Server.Wires;
using Content.Shared.VendingMachines;
using Content.Shared.VendingMachines.Components;
using Content.Shared.Wires;

namespace Content.Server.VendingMachines.WireActions;

[DataDefinition]
public sealed partial class VendingMachineContrabandWireAction : BaseToggleWireAction
{
    public override Color Color { get; set; } = Color.Green;
    public override string Name { get; set; } = "wire-name-vending-contraband";
    public override object? StatusKey { get; } = ContrabandWireKey.StatusKey;
    public override object? TimeoutKey { get; } = ContrabandWireKey.TimeoutKey;

    public override StatusLightState? GetLightState(Wire wire)
    {
        if (EntityManager.TryGetComponent(wire.Owner, out VendingMachineInventoryComponent? vending))
        {
            return vending.Contraband
                ? StatusLightState.BlinkingSlow
                : StatusLightState.On;
        }

        return StatusLightState.Off;
    }

    public override void ToggleValue(EntityUid owner, bool setting)
    {
        if (EntityManager.TryGetComponent(owner, out VendingMachineInventoryComponent? vending))
        {
            vending.Contraband = !setting;
        }
    }

    public override bool GetValue(EntityUid owner)
    {
        return EntityManager.TryGetComponent(owner, out VendingMachineInventoryComponent? vending) && !vending.Contraband;
    }
}
