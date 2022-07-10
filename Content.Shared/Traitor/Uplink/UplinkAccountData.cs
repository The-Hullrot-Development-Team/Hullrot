using Content.Shared.Roles;
using Robust.Shared.Serialization;

namespace Content.Shared.Traitor.Uplink
{
    [Serializable, NetSerializable]
    public sealed class UplinkAccountData
    {
        public EntityUid? DataAccountHolder;
        public int DataBalance;
        public HashSet<JobPrototype>? JobWhitelist;
        public UplinkAccountData(EntityUid? dataAccountHolder, int dataBalance, HashSet<JobPrototype>? jobWhitelist = null)
        {
            DataAccountHolder = dataAccountHolder;
            DataBalance = dataBalance;
            JobWhitelist = jobWhitelist;
        }
    }
}
