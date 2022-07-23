﻿using Content.Server.Atmos.EntitySystems;

namespace Content.Server.Atmos.Reactions;

public sealed class MiasmicSubsumationReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem)
    {
        return ReactionResult.Reacting;
    }
}
