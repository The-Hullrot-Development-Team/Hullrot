using System;
using System.Collections.Generic;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.DragDrop;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.MedicalScanner
{
    public abstract class SharedMedicalScannerComponent : Component, IDragDropOn
    {
        public override string Name => "MedicalScanner";

        [Serializable, NetSerializable]
        public class MedicalScannerBoundUserInterfaceState : BoundUserInterfaceState
        {
            public readonly EntityUid? Entity;
            public readonly Dictionary<string, int> DamageGroupIDs;
            public readonly Dictionary<string, int> DamageTypeIDs;
            public readonly bool IsScanned;

            // TODO QUESTION DrSmugleaf previously commented here saying that these dicitionaries should be using
            // strings, instead when they were previously <DamageGroupPrototype, int> dictionaries. I think I've made
            // all the needed changes, but have no idea how the networking or UI work. The medicalscanner works when
            // testing locally with two local clients, so hopefully this is what I was supposed to do?
            public MedicalScannerBoundUserInterfaceState(
                EntityUid? entity,
                Dictionary<string, int> damageGroupIDs,
                Dictionary<string, int> damageTypesIDs,
                bool isScanned)
            {
                Entity = entity;
                DamageGroupIDs = damageGroupIDs;
                DamageTypeIDs = damageTypesIDs;
                IsScanned = isScanned;
            }

            public bool HasDamage()
            {
                return DamageGroupIDs.Count > 0 || DamageTypeIDs.Count > 0;
            }
        }

        [Serializable, NetSerializable]
        public enum MedicalScannerUiKey
        {
            Key
        }

        [Serializable, NetSerializable]
        public enum MedicalScannerVisuals
        {
            Status
        }

        [Serializable, NetSerializable]
        public enum MedicalScannerStatus
        {
            Off,
            Open,
            Red,
            Death,
            Green,
            Yellow,
        }

        [Serializable, NetSerializable]
        public enum UiButton
        {
            ScanDNA,
        }

        [Serializable, NetSerializable]
        public class UiButtonPressedMessage : BoundUserInterfaceMessage
        {
            public readonly UiButton Button;

            public UiButtonPressedMessage(UiButton button)
            {
                Button = button;
            }
        }


        bool IDragDropOn.CanDragDropOn(DragDropEvent eventArgs)
        {
            return eventArgs.Dragged.HasComponent<SharedBodyComponent>();
        }

        public abstract bool DragDropOn(DragDropEvent eventArgs);
    }
}
