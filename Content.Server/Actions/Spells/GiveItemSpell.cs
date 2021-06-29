using Content.Server.Hands.Components;
using Content.Server.Items;
using Content.Server.Notification;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions.Behaviors;
using Content.Shared.Actions.Components;
using Content.Shared.Cooldown;
using Content.Shared.Notification.Managers;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Server.Actions
{
    [UsedImplicitly]
    [DataDefinition]
    public class GiveItemSpell : IInstantAction
    {
        [ViewVariables] [DataField("castMessage")] public string? CastMessage { get; set; } = default!;
        [ViewVariables] [DataField("coolDown")] public float CoolDown { get; set; } = 1f;
        [ViewVariables] [DataField("spellItem")] public string ItemProto { get; set; } = default!;

        [ViewVariables] [DataField("castSound")] public string? CastSound { get; set; } = default!;

        //Rubber-band snapping items into player's hands, originally was a workaround, later found it works quite well with stuns
        //Not sure if needs fixing

        public void DoInstantAction(InstantActionEventArgs args)
        {
            if (!args.Performer.TryGetComponent<SharedActionsComponent>(out var actions)) return;
            var caster = args.Performer;
            var casterCoords = caster.Transform.Coordinates;
            var spawnedProto = caster.EntityManager.SpawnEntity(ItemProto, casterCoords);
            //Checks if caster can perform the action
            if (!caster.TryGetComponent<HandsComponent>(out var hands))
            {
                caster.PopupMessage("You don't have hands!");
                return;
            }
            if (!EntitySystem.Get<ActionBlockerSystem>().CanInteract(caster)) return;
            //Perfrom the action
            args.PerformerActions?.Cooldown(args.ActionType, Cooldowns.SecondsFromNow(CoolDown));
            if (CastMessage != null) caster.PopupMessageEveryone(CastMessage);
            caster.GetComponent<HandsComponent>().PutInHandOrDrop(spawnedProto.GetComponent<ItemComponent>(), true);
            if (CastSound != null)
            {
                SoundSystem.Play(Filter.Pvs(caster), CastSound, caster);
            }
        }
    } 
}
