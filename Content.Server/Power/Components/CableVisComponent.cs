﻿namespace Content.Server.Power.Components
{
    [RegisterComponent]
    public sealed partial class CableVisComponent : Component
    {
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField(required:true)]
        public string Node;
    }
}
