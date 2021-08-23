using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using System;

namespace Content.Server.Flash.Components
{
    /// <summary>
    /// Upon being triggered will flash in an area around it.
    /// </summary>
    [RegisterComponent]
    internal sealed class FlashOnTriggerComponent : Component
    {
        public override string Name => "FlashOnTrigger";

        [DataField("range")] internal float Range = 1.0f;
        [DataField("duration")] internal float Duration = 8.0f;

        internal bool Flashed;

        [DataField("repeating")] internal bool Repeating = false;
        [DataField("cooldown")] internal int Cooldown = 4;

        internal TimeSpan LastFlash = TimeSpan.Zero;
    }
}
