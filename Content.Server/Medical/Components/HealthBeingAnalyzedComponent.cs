using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server.Medical.Components;

[RegisterComponent]
public sealed partial class HealthBeingAnalyzedComponent : Component
{
    /// <summary>
    /// Set of health analyzers currently monitoring this component's parent entity
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public HashSet<EntityUid> ActiveAnalyzers = new HashSet<EntityUid>();
}
