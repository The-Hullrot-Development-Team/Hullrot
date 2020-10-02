﻿using System;
using System.Collections.Generic;
using Content.Shared.Audio;
using Content.Shared.Interfaces;
using Robust.Shared.Interfaces.Serialization;
using Robust.Shared.IoC;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Construction
{
    [Serializable, NetSerializable]
    public abstract class ConstructionGraphStep : IExposeData
    {
        private List<IStepCompleted> _completed;
        public float DoAfter { get; private set; }
        public string Sound { get; private set; }
        public string SoundCollection { get; private set; }
        public string Popup { get; private set; }
        public IReadOnlyList<IStepCompleted> Completed => _completed;

        public virtual void ExposeData(ObjectSerializer serializer)
        {
            var moduleManager = IoCManager.Resolve<IModuleManager>();

            serializer.DataField(this, x => x.DoAfter, "doAfter", 0f);
            serializer.DataField(this, x => x.Sound, "sound", string.Empty);
            serializer.DataField(this, x => x.SoundCollection, "soundCollection", string.Empty);
            if (!moduleManager.IsServerModule) return;
            serializer.DataField(ref _completed, "completed", new List<IStepCompleted>());
        }

        public abstract void DoExamine(FormattedMessage message, bool inDetailsRange);

        public string GetSound()
        {
            return !string.IsNullOrEmpty(SoundCollection) ? AudioHelpers.GetRandomFileFromSoundCollection(SoundCollection) : Sound;
        }
    }
}
