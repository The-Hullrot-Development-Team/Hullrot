﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Reflection;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.ViewVariables;

namespace Content.Server.GameObjects.Components.Power
{
    /// <summary>
    /// Master class for group of <see cref="PowerTransferComponent"/>, responsible for connecting devices and network-specific managers.
    /// Like with other subsystems, the powernet managers are automatically created based on classes.
    /// </summary>
    public class Powernet
    {
        public Powernet()
        {
            var EntitySystemManager = IoCManager.Resolve<IEntitySystemManager>();
            var powerSystem = EntitySystemManager.GetEntitySystem<PowerSystem>();
            powerSystem.Powernets.Add(this);
            Uid = powerSystem.NewUid();

            var ReflectionManager = IoCManager.Resolve<IReflectionManager>();
            foreach (Type t in ReflectionManager.GetAllChildren<PowernetManager>())
            {
                ConstructorInfo c = t.GetConstructor(new Type[] {typeof(Powernet)});
                if (c != null)
                {
                    Managers.Add((PowernetManager) c.Invoke(new object[] {this}));
                }
            }

            foreach (PowernetManager manager in Managers)
            {
                manager.Initialize();
            }
        }

        /// <summary>
        /// Managers specific to this network.
        /// An example would be <see cref="PowernetPowerManager"/>.
        /// Do note that these managers expect to not be removed/added.
        /// And users of these managers do not expect GetManager<>() to return null.
        /// </summary>
        public readonly List<PowernetManager> Managers = new List<PowernetManager>();

        /// <summary>
        ///     Unique identifier per wirenet, used for debugging mostly.
        /// </summary>
        [ViewVariables]
        public int Uid { get; }

        /// <summary>
        ///     The entities that make up the powernet's physical location and allow powernet connection
        /// </summary>
        public readonly List<PowerTransferComponent> WireList = new List<PowerTransferComponent>();

        /// <summary>
        ///     Entities that connect directly to the powernet through <see cref="PowerTransferComponent" /> above to add power or add power load
        /// </summary>
        public readonly List<PowerNodeComponent> NodeList = new List<PowerNodeComponent>();

        /// <summary>
        ///     Variable that causes powernet to be regenerated from its wires during the next update cycle.
        /// </summary>
        [ViewVariables]
        public bool Dirty { get; set; } = false;

        /// <summary>
        /// Returns a powernet manager by type, or null.
        /// Be aware that it isn't possible for this to return null for any
        /// existing non-abstract constructable PowernetManager subclass.
        /// Of course, if a PowernetManager subclass is abstract or unconstructable?
        /// That's different...
        /// </summary>
        public T GetManager<T>() where T : PowernetManager
        {
            foreach (PowernetManager manager in Managers)
            {
                if (manager is T)
                {
                    return (T) manager;
                }
            }
            return null;
        }

        public void Update(float frameTime)
        {
            foreach (PowernetManager manager in Managers)
            {
                manager.Update(frameTime);
            }
        }

        /// <summary>
        /// Kills a wirenet after it is marked dirty and its component have already been regenerated by the powernet system
        /// </summary>
        public void DirtyKill()
        {
            WireList.Clear();
            while (NodeList.Count != 0)
            {
                NodeList[0].DisconnectFromPowernet();
            }
            RemoveFromSystem();
        }

        /// <summary>
        /// Combines two powernets when they connect via powertransfer components
        /// </summary>
        public void MergePowernets(Powernet toMerge)
        {
            //TODO: load balance reconciliation between powernets on merge tick here

            foreach (var wire in toMerge.WireList)
            {
                wire.Parent = this;
            }

            WireList.AddRange(toMerge.WireList);
            toMerge.WireList.Clear();

            foreach (var node in toMerge.NodeList)
            {
                node.Parent = this;
            }

            NodeList.AddRange(toMerge.NodeList);
            toMerge.NodeList.Clear();

            foreach (var manager in Managers)
            {
                manager.MergeFrom(toMerge);
            }

            toMerge.RemoveFromSystem();
        }

        /// <summary>
        /// Removes reference from the powernets list on the powernet system
        /// </summary>
        private void RemoveFromSystem()
        {
            var EntitySystemManager = IoCManager.Resolve<IEntitySystemManager>();
            EntitySystemManager.GetEntitySystem<PowerSystem>().Powernets.Remove(this);
        }

        public override string ToString()
        {
            return $"Powernet {Uid}";
        }
    }
}
