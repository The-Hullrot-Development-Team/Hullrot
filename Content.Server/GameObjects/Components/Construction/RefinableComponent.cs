﻿using System.Threading.Tasks;
using Content.Server.GameObjects.Components.Interactable;
using Content.Server.GameObjects.Components.Stack;
using Content.Shared.GameObjects.Components.Interactable;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;

namespace Content.Server.GameObjects.Components.Construction
{
    /// <summary>
    /// Used for something that can be refined by welder.
    /// For example, glass shard can be refined to glass sheet.
    /// </summary>
    [RegisterComponent]
    public class RefinableComponent : Component, IInteractUsing
    {
        [ViewVariables]
        private string[] _refineResult;

        public override string Name => "Refinable";

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataField(ref _refineResult, "refineResult", new string[] { "GlassStack" });
        }

        public async Task<bool> InteractUsing(InteractUsingEventArgs eventArgs)
        {
            // check if object is welder
            if (!eventArgs.Using.TryGetComponent(out ToolComponent tool)) return false;
            if (!await tool.UseTool(eventArgs.User, Owner, 2, ToolQuality.Welding)) return false;

            Owner.Delete();

            // spawn each result afrer refine
            foreach (var result in _refineResult)
            {
                var droppedEnt = Owner.EntityManager.SpawnEntity(result, eventArgs.ClickLocation);

                if (droppedEnt.TryGetComponent<StackComponent>(out var stackComp))
                    stackComp.Count = 1;
            }

            return true;
        }
    }
}
