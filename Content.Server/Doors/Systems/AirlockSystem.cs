﻿using Content.Server.Doors.Components;
using Content.Server.Power.Components;
using Content.Server.WireHacking;
using Content.Shared.Doors;
using Content.Shared.Popups;
using Content.Shared.Interaction;
using Content.Shared.Remotes;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;

namespace Content.Server.Doors.Systems
{
    public class AirlockSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<AirlockComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<AirlockComponent, DoorStateChangedEvent>(OnStateChanged);
            SubscribeLocalEvent<AirlockComponent, BeforeDoorOpenedEvent>(OnBeforeDoorOpened);
            SubscribeLocalEvent<AirlockComponent, BeforeDoorClosedEvent>(OnBeforeDoorClosed);
            SubscribeLocalEvent<AirlockComponent, BeforeDoorDeniedEvent>(OnBeforeDoorDenied);
            SubscribeLocalEvent<AirlockComponent, DoorSafetyEnabledEvent>(OnDoorSafetyCheck);
            SubscribeLocalEvent<AirlockComponent, BeforeDoorAutoCloseEvent>(OnDoorAutoCloseCheck);
            SubscribeLocalEvent<AirlockComponent, DoorGetCloseTimeModifierEvent>(OnDoorCloseTimeModifier);
            SubscribeLocalEvent<AirlockComponent, DoorClickShouldActivateEvent>(OnDoorClickShouldActivate);
            SubscribeLocalEvent<AirlockComponent, BeforeDoorPryEvent>(OnDoorPry);
            SubscribeLocalEvent<AirlockComponent, RangedInteractEvent>(OnRangedInteract);
        }

        private void OnPowerChanged(EntityUid uid, AirlockComponent component, PowerChangedEvent args)
        {
            if (TryComp<AppearanceComponent>(uid, out var appearanceComponent))
            {
                appearanceComponent.SetData(DoorVisuals.Powered, args.Powered);
            }

            // BoltLights also got out
            component.UpdateBoltLightStatus();
        }

        private void OnStateChanged(EntityUid uid, AirlockComponent component, DoorStateChangedEvent args)
        {
            // Only show the maintenance panel if the airlock is closed
            if (TryComp<WiresComponent>(uid, out var wiresComponent))
            {
                wiresComponent.IsPanelVisible =
                    component.OpenPanelVisible
                    ||  args.State != SharedDoorComponent.DoorState.Open;
            }
            // If the door is closed, we should look if the bolt was locked while closing
            component.UpdateBoltLightStatus();
        }

        private void OnBeforeDoorOpened(EntityUid uid, AirlockComponent component, BeforeDoorOpenedEvent args)
        {
            if (!component.CanChangeState())
                args.Cancel();
        }

        private void OnBeforeDoorClosed(EntityUid uid, AirlockComponent component, BeforeDoorClosedEvent args)
        {
            if (!component.CanChangeState())
                args.Cancel();
        }

        private void OnBeforeDoorDenied(EntityUid uid, AirlockComponent component, BeforeDoorDeniedEvent args)
        {
            if (!component.CanChangeState())
                args.Cancel();
        }

        private void OnDoorSafetyCheck(EntityUid uid, AirlockComponent component, DoorSafetyEnabledEvent args)
        {
            args.Safety = component.Safety;
        }

        private void OnDoorAutoCloseCheck(EntityUid uid, AirlockComponent component, BeforeDoorAutoCloseEvent args)
        {
            if (!component.AutoClose)
                args.Cancel();
        }

        private void OnDoorCloseTimeModifier(EntityUid uid, AirlockComponent component, DoorGetCloseTimeModifierEvent args)
        {
            args.CloseTimeModifier *= component.AutoCloseDelayModifier;
        }

        private void OnDoorClickShouldActivate(EntityUid uid, AirlockComponent component, DoorClickShouldActivateEvent args)
        {
            if (TryComp<WiresComponent>(uid, out var wiresComponent) && wiresComponent.IsPanelOpen &&
                EntityManager.TryGetComponent(args.Args.User, out ActorComponent? actor))
            {
                wiresComponent.OpenInterface(actor.PlayerSession);
                args.Handled = true;
            }
        }

        private void OnDoorPry(EntityUid uid, AirlockComponent component, BeforeDoorPryEvent args)
        {
            if (component.IsBolted())
            {
                component.Owner.PopupMessage(args.Args.User, Loc.GetString("airlock-component-cannot-pry-is-bolted-message"));
                args.Cancel();
            }
            if (component.IsPowered())
            {
                component.Owner.PopupMessage(args.Args.User, Loc.GetString("airlock-component-cannot-pry-is-powered-message"));
                args.Cancel();
            }
        }

        private void OnRangedInteract(EntityUid uid, AirlockComponent component, RangedInteractEvent args)
        {
            args.Handled = true;
            var doorComponent = EntityManager.GetComponent<ServerDoorComponent>(component.Owner);
            
            // If it isn't a door remote we don't use it
            if(!EntityManager.TryGetComponent<DoorRemoteComponent?>(args.UsedUid, out var remoteComponent))
            {
                args.Handled = false;
                return;
            }

            if (remoteComponent.Mode == DoorRemoteComponent.OperatingMode.OpenClose)
            {
                if (doorComponent.State == SharedDoorComponent.DoorState.Open)
                {
                    doorComponent.TryClose(args.UsedUid);
                }
                else if (doorComponent.State == SharedDoorComponent.DoorState.Closed)
                {
                    doorComponent.TryOpen(args.UsedUid);
                }
            }

            if (remoteComponent.Mode == DoorRemoteComponent.OperatingMode.ToggleBolts
                    && component.IsPowered())
            {
                if(component.IsBolted())
                {
                    component.SetBoltsWithAudio(false);
                }
                else
                {
                    component.SetBoltsWithAudio(true);
                }
            }
        }
    }
}
