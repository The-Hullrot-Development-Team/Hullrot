﻿using Content.Server.GameObjects.Components.NodeContainer.NodeGroups;
using Content.Server.GameObjects.Components.Power.ApcNetComponents;
using Robust.Shared.Interfaces.GameObjects;
using System.Collections.Generic;

namespace Content.Server.GameObjects.EntitySystems.DeviceNetwork
{
    public class WiredNetworkConnection : BaseNetworkConnection
    {
        private readonly IEntity _owner;
        private readonly PowerReceiverComponent _powerReceiver;

        public WiredNetworkConnection(OnReceiveNetMessage onReceive, bool receiveAll, IEntity owner) : base(1, 0, onReceive, receiveAll)
        {
            _owner = owner;

            if (_owner.TryGetComponent<PowerReceiverComponent>(out var powerReceiver))
            {
                _powerReceiver = powerReceiver;
            }
        }

        protected override bool CanReceive(int frequency, string sender, IReadOnlyDictionary<string, string> payload, Metadata metadata, bool broadcast)
        {

            if (_powerReceiver.TryGetHVNodeGroup(out var ownNet) && metadata.TryParseMetadata<IPowerNet>("powernet", out var senderNet))
            {
                return ownNet.Equals(senderNet);
            }

            return false;
        }

        protected override Metadata GetMetadata()
        {
            if (_powerReceiver.TryGetHVNodeGroup(out var net))
            {
                var metadata = new Metadata
                {
                    {"powernet", net }
                };

                return metadata;
            }

            return new Metadata();
        }

        protected override Dictionary<string, string> ManipulatePayload(Dictionary<string, string> payload)
        {
            return payload;
        }
    }
}
