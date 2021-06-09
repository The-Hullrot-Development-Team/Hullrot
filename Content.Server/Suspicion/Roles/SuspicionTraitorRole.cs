using System.Collections.Generic;
using System.Linq;
using Content.Server.Chat.Managers;
using Content.Shared.Roles;
using Robust.Shared.Localization;

namespace Content.Server.Suspicion.Roles
{
    public sealed class SuspicionTraitorRole : SuspicionRole
    {
        public AntagPrototype Prototype { get; }

        public SuspicionTraitorRole(Mind.Mind mind, AntagPrototype antagPrototype) : base(mind)
        {
            Prototype = antagPrototype;
            Name = antagPrototype.Name;
            Antagonist = antagPrototype.Antagonist;
        }

        public override string Name { get; }
        public string Objective => Prototype.Objective;
        public override bool Antagonist { get; }

        public void GreetSuspicion(List<SuspicionTraitorRole> traitors, IChatManager chatMgr)
        {
            if (Mind.TryGetSession(out var session))
            {
                chatMgr.DispatchServerMessage(session, Loc.GetString("suspicion-role-greeting", ("roleName", Name)));
                chatMgr.DispatchServerMessage(session, Loc.GetString("suspicion-objective", ("objectiveText", Objective)));

                var allPartners = string.Join(", ", traitors.Where(p => p != this).Select(p => p.Mind.CharacterName));

                var partnerText = Loc.GetString(
                    "suspicion-partners-in-crime",
                    ("partnerCount", traitors.Count-1),
                    ("partnerNames", allPartners)
                );

                chatMgr.DispatchServerMessage(session, partnerText);
            }
        }
    }
}
