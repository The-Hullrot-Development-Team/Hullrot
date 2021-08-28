using Content.Shared.Traitor.Uplink;
using Robust.Shared.GameObjects;

namespace Content.Server.Traitor.Uplink.Events
{
    public class TryInitUplinkEvent : EntityEventArgs
    {
        public UplinkAccount Account;

        public TryInitUplinkEvent(UplinkAccount account)
        {
            Account = account;
        }
    }
}
