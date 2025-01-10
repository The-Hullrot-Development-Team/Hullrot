using Content.Server.Chat.Systems;
using Content.Server.Mind.Commands;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Magic;
using Content.Shared.Magic.Events;

namespace Content.Server.Magic;

public sealed class MagicSystem : SharedMagicSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpeakSpellEvent>(OnSpellSpoken);
    }

    private void OnSpellSpoken(ref SpeakSpellEvent args)
    {
        _chat.TrySendInGameICMessage(args.Performer, Loc.GetString(args.Speech), InGameICChatType.Speak, false);
    }

    public override void AnimateSpellHelper(AnimateSpellEvent ev)
    {
        MakeSentientCommand.MakeSentient(ev.Target, EntityManager, true, true);

        var npc = EnsureComp<HTNComponent>(ev.Target);
        npc.RootTask = new HTNCompoundTask()
        {
            Task = ev.Task
        };
        
    }
}
