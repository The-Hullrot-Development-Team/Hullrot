using Content.Shared.Silicons.StationAi;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.StationAi;

public sealed class StationAiSystem : SharedStationAiSystem
{
    [Dependency] private readonly IOverlayManager _overlayMgr = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private StationAiOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationAiOverlayComponent, PlayerAttachedEvent>(OnAiAttached);
        SubscribeLocalEvent<StationAiOverlayComponent, PlayerDetachedEvent>(OnAiDetached);
        SubscribeLocalEvent<StationAiOverlayComponent, ComponentInit>(OnAiOverlayInit);
        SubscribeLocalEvent<StationAiOverlayComponent, ComponentRemove>(OnAiOverlayRemove);
    }

    private void OnAiOverlayInit(Entity<StationAiOverlayComponent> ent, ref ComponentInit args)
    {
        var attachedEnt = _player.LocalEntity;

        if (attachedEnt != ent.Owner)
            return;

        AddOverlay();
    }

    private void OnAiOverlayRemove(Entity<StationAiOverlayComponent> ent, ref ComponentRemove args)
    {
        var attachedEnt = _player.LocalEntity;

        if (attachedEnt != ent.Owner)
            return;

        RemoveOverlay();
    }

    private void AddOverlay()
    {
        if (_overlay != null)
            return;

        _overlay = new StationAiOverlay();
        _overlayMgr.AddOverlay(_overlay);
    }

    private void RemoveOverlay()
    {
        if (_overlay == null)
            return;

        _overlayMgr.RemoveOverlay(_overlay);
        _overlay = null;
    }

    private void OnAiAttached(Entity<StationAiOverlayComponent> ent, ref PlayerAttachedEvent args)
    {
        AddOverlay();
    }

    private void OnAiDetached(Entity<StationAiOverlayComponent> ent, ref PlayerDetachedEvent args)
    {
        RemoveOverlay();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMgr.RemoveOverlay<StationAiOverlay>();
    }
}
