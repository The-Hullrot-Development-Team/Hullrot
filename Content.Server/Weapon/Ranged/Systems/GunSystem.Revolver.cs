using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server.Weapon.Ranged.Systems;

public sealed partial class GunSystem
{
    protected override void SpinRevolver(RevolverAmmoProviderComponent component, EntityUid? user = null)
    {
        PlaySound(component.Owner, component.SoundSpin?.GetSound(Random, ProtoManager), user);
        var index = Random.Next(component.Capacity);

        if (component.CurrentIndex == index) return;

        component.CurrentIndex = index;
        Dirty(component);
    }
}
