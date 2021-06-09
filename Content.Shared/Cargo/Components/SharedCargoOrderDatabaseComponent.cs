﻿#nullable enable
using System;
using System.Collections.Generic;
using Content.Shared.NetIDs;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Components
{
    public class SharedCargoOrderDatabaseComponent : Component
    {
        public sealed override string Name => "CargoOrderDatabase";
        public sealed override uint? NetID => ContentNetIDs.CARGO_ORDER_DATABASE;
    }

    [NetSerializable, Serializable]
    public class CargoOrderDatabaseState : ComponentState
    {
        public readonly List<CargoOrderData>? Orders;

        public CargoOrderDatabaseState(List<CargoOrderData>? orders) : base(ContentNetIDs.CARGO_ORDER_DATABASE)
        {
            Orders = orders;
        }
    }
}
