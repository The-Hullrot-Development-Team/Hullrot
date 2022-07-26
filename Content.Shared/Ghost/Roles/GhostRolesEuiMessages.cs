using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Ghost.Roles
{
    [NetSerializable, Serializable]
    public struct GhostRoleInfo
    {
        public uint Identifier { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Rules { get; set; }
        public TimeSpan ExpiresAt { get; set; }
        public TimeSpan AddedAt { get; set; }
        public int NumSignUps { get; set; }
        public bool IsRequested { get; set; }
    }

    [NetSerializable, Serializable]
    public sealed class GhostRolesEuiState : EuiStateBase
    {
        public GhostRoleInfo[] GhostRoles { get; }

        public GhostRolesEuiState(GhostRoleInfo[] ghostRoles)
        {
            GhostRoles = ghostRoles;
        }
    }

    [NetSerializable, Serializable]
    public sealed class GhostRoleTakeoverRequestMessage : EuiMessageBase
    {
        public uint Identifier { get; }

        public GhostRoleTakeoverRequestMessage(uint identifier)
        {
            Identifier = identifier;
        }
    }

    [NetSerializable, Serializable]
    public sealed class GhostRoleCancelTakeoverRequestMessage : EuiMessageBase
    {
        public uint Identifier { get; }

        public GhostRoleCancelTakeoverRequestMessage(uint identifier)
        {
            Identifier = identifier;
        }
    }

    [NetSerializable, Serializable]
    public sealed class GhostRoleFollowRequestMessage : EuiMessageBase
    {
        public uint Identifier { get; }

        public GhostRoleFollowRequestMessage(uint identifier)
        {
            Identifier = identifier;
        }
    }

    [NetSerializable, Serializable]
    public sealed class GhostRoleWindowCloseMessage : EuiMessageBase
    {
    }
}
