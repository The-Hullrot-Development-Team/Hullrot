using Content.Server.Chat.Systems;
using Content.Server.Speech.Components;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Speech.Components;

namespace Content.Server.Speech.EntitySystems;

public sealed class MumbleAccentSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MumbleAccentComponent, AccentGetEvent>(OnAccentGet);
        SubscribeLocalEvent<MumbleAccentComponent, EmoteEvent>(OnEmote, before: [typeof(VocalSystem)]);
    }

    private void OnEmote(EntityUid uid, MumbleAccentComponent component, ref EmoteEvent args)
    {
        if (args.Handled || !args.Emote.Category.HasFlag(EmoteCategory.Vocal))
            return;

        if (TryComp<VocalComponent>(uid, out var vocalComp))
        {
            // play a muffled version of the vocal emote
            args.Handled = _chat.TryPlayEmoteSound(uid, vocalComp.EmoteSounds, args.Emote, component.EmoteAudioParams);
        }
    }

    public string Accentuate(string message, MumbleAccentComponent component)
    {
        return _replacement.ApplyReplacements(message, "mumble");
    }

    private void OnAccentGet(Entity<MumbleAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message, ent.Comp);
    }
}
