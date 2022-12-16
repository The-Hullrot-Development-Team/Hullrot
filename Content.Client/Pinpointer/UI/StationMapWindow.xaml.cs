using Content.Client.UserInterface.Controls;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;

namespace Content.Client.Pinpointer.UI;

[GenerateTypedNameReferences]
public sealed partial class StationMapWindow : FancyWindow
{
    public StationMapWindow(EntityUid? mapUid, EntityUid? trackedEntity)
    {
        RobustXamlLoader.Load(this);
        NavMapScreen.MapUid = mapUid;

        if (trackedEntity != null)
            NavMapScreen.TrackedCoordinates.Add(new (new EntityCoordinates(trackedEntity.Value, Vector2.Zero), Color.Red));

        if (IoCManager.Resolve<IEntityManager>().TryGetComponent<MetaDataComponent>(mapUid, out var metadata))
        {
            Title = metadata.EntityName;
        }
    }
}
