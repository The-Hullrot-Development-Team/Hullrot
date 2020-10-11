﻿#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Server.Atmos;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.Components.Atmos
{
    public class SpaceGridAtmosphereComponent : UnsimulatedGridAtmosphereComponent
    {
        public override string Name => "SpaceGridAtmosphere";

        public override void RepopulateTiles() { }

        public override bool IsSpace(Vector2i indices)
        {
            return true;
        }

        public override TileAtmosphere? GetTile(Vector2i indices, bool createSpace = true)
        {
            return new TileAtmosphere(this, GridId.Invalid, indices, new GasMixture(2500, AtmosphereSystem), true);
        }

        protected override IEnumerable<AirtightComponent> GetObstructingComponents(Vector2i indices)
        {
            return Enumerable.Empty<AirtightComponent>();
        }
    }
}
