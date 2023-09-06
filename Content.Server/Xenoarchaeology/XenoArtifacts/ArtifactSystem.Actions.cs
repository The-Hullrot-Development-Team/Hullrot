﻿using Content.Server.Actions;
using Content.Server.Popups;
using Content.Shared.Xenoarchaeology.XenoArtifacts;
using Robust.Shared.Prototypes;

namespace Content.Server.Xenoarchaeology.XenoArtifacts;

public partial class ArtifactSystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    [ValidatePrototypeId<EntityPrototype>] private const string ArtifactActivateActionId = "ArtifactActivate";

    /// <summary>
    ///     Used to add the artifact activation action (hehe), which lets sentient artifacts activate themselves,
    ///     either through admemery or the sentience effect.
    /// </summary>
    public void InitializeActions()
    {
        SubscribeLocalEvent<ArtifactComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ArtifactComponent, ComponentRemove>(OnRemove);

        SubscribeLocalEvent<ArtifactComponent, ArtifactSelfActivateEvent>(OnSelfActivate);
    }

    private void OnStartup(EntityUid uid, ArtifactComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, Spawn(ArtifactActivateActionId), null);
    }

    private void OnRemove(EntityUid uid, ArtifactComponent component, ComponentRemove args)
    {
        _actions.RemoveAction(uid, ArtifactActivateActionId);
    }

    private void OnSelfActivate(EntityUid uid, ArtifactComponent component, ArtifactSelfActivateEvent args)
    {
        if (component.CurrentNodeId == null)
            return;

        var curNode = GetNodeFromId(component.CurrentNodeId.Value, component).Id;
        _popup.PopupEntity(Loc.GetString("activate-artifact-popup-self", ("node", curNode)), uid, uid);
        TryActivateArtifact(uid, uid, component);

        args.Handled = true;
    }
}
