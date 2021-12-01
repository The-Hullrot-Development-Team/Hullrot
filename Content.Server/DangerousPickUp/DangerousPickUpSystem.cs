using System;
using Content.Shared.DangerousPickUp.Components;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Server.Clothing.Components;
using Content.Server.Inventory.Components;
using Content.Server.Items;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;

namespace Content.Shared.DangerousPickUp
{
    public class DangerousPickUpSystem : EntitySystem
    {
        
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DangerousPickUpComponent, EquippedHandEvent>(OnEquippedHand);

        }
        

        private void OnEquippedHand(EntityUid uid, DangerousPickUpComponent component, EquippedHandEvent args)
        {
            Logger.Info("Received equipped hand event");
            string glovesType;
            glovesType  = TryGetGlovesType(args.User);
            Logger.Info("Received glove type" + glovesType + "from TryGetGlovesType");
            if(!args.User.TryGetComponent<SharedHandsComponent>(out var handComponent))
            return;
            
            if (glovesType != component.dangerType)
            {
               Logger.Info("Trying to drop...");
               handComponent.TryDropEntity(args.Equipped,args.User.Transform.Coordinates,true);
            }
        }
        
        
        
        private string TryGetGlovesType(IEntity user)
        {
            if (!user.TryGetComponent<InventoryComponent>(out var inventoryComp))
            {
                Logger.Info("Trying to fetch inventory component...");
                return string.Empty;
            }

            if (inventoryComp.TryGetSlotItem(EquipmentSlotDefines.Slots.GLOVES, out ItemComponent? gloves))
            {
                Logger.Info("Trying to fetch gloves item component...");
                if (gloves.Owner.TryGetComponent(out DangerousPickUpProtectionComponent? glovesProtection))
                {
                        Logger.Info("Trying to extract glove type...");
                        return glovesProtection?.protectionType ?? string.Empty;
                }
            }
            return string.Empty;
        }
    }
}
