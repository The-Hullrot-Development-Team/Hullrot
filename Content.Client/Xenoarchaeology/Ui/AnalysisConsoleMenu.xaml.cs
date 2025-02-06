using System.Linq;
using System.Text;
using Content.Client.Message;
using Content.Client.Resources;
using Content.Client.UserInterface.Controls;
using Content.Client.Xenoarchaeology.Artifact;
using Content.Client.Xenoarchaeology.Equipment;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Xenoarchaeology.Equipment.Components;
using Robust.Client.Audio;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Audio;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Xenoarchaeology.Ui;

[GenerateTypedNameReferences]
public sealed partial class AnalysisConsoleMenu : FancyWindow
{
    /// <summary> Time for which extract point messages going to stay on display before screen clear. </summary>
    private static readonly TimeSpan ExtractNonEmptyShowDelaySpan = TimeSpan.FromSeconds(3);
    /// <summary> Time for which zero extracted points message is going to stay on display before screen clear. </summary>
    private static readonly TimeSpan ExtractEmptyShowDelaySpan = TimeSpan.FromSeconds(0.25);

    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly IResourceCache _resCache = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly ArtifactAnalyzerSystem _artifactAnalyzer;
    private readonly XenoArtifactSystem _xenoArtifact;
    private readonly AudioSystem _audio;

    private readonly Entity<AnalysisConsoleComponent> _owner;
    private Entity<XenoArtifactNodeComponent>? _currentNode;

    /// <summary> Queue of node info to output into extraction window. </summary>
    private readonly List<(string NodeId, int ExtractedPoints)> _nodeExtractionsToProcess = new();
    private TimeSpan? _nextExtractStringTime;
    private int _extractionSum;
    private readonly FormattedMessage _extractionMessage = new();

    public event Action? OnServerSelectionButtonPressed;
    public event Action? OnExtractButtonPressed;

    public AnalysisConsoleMenu(EntityUid owner)
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        _xenoArtifact = _ent.System<XenoArtifactSystem>();
        _artifactAnalyzer = _ent.System<ArtifactAnalyzerSystem>();
        _audio = _ent.System<AudioSystem>();

        if (BackPanel.PanelOverride is StyleBoxTexture tex)
            tex.Texture = _resCache.GetTexture("/Textures/Interface/Nano/button.svg.96dpi.png");

        InitStaticLabels();

        GraphControl.OnNodeSelected += node =>
        {
            _currentNode = node;
            SetSelectedNode(node);
        };

        ServerButton.OnPressed += _ =>
        {
            OnServerSelectionButtonPressed?.Invoke();
        };

        ExtractButton.OnPressed += StartExtract;

        var comp = _ent.GetComponent<AnalysisConsoleComponent>(owner);
        _owner = (owner, comp);
        Update(_owner);
    }

    private void StartExtract(BaseButton.ButtonEventArgs obj)
    {
        if (!_artifactAnalyzer.TryGetArtifactFromConsole(_owner, out var artifact))
            return;

        ExtractContainer.Visible = true;
        NodeViewContainer.Visible = false;

        _nodeExtractionsToProcess.Clear();
        _extractionSum = 0;
        _extractionMessage.Clear();
        _nextExtractStringTime = _timing.CurTime;

        var nodes = _xenoArtifact.GetAllNodes(artifact.Value);
        foreach (var node in nodes)
        {
            var pointValue = _xenoArtifact.GetResearchValue(node);
            if (pointValue <= 0)
                continue;

            var nodeId = _xenoArtifact.GetNodeId(node);

            var text = Loc.GetString("analysis-console-extract-value", ("id", nodeId), ("value", pointValue));
            _nodeExtractionsToProcess.Add((text, pointValue));
        }

        if (_nodeExtractionsToProcess.Count == 0)
            _nodeExtractionsToProcess.Add((Loc.GetString("analysis-console-extract-none"), 0));

        _nodeExtractionsToProcess.Sort((x, y) => x.ExtractedPoints.CompareTo(y.ExtractedPoints));
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_nextExtractStringTime == null || _timing.CurTime < _nextExtractStringTime)
            return;

        if (_nodeExtractionsToProcess.Count == 0)
        {
            ExtractContainer.Visible = false;
            NodeViewContainer.Visible = true;
            _nextExtractStringTime = null;

            return;
        }

        var (message, value) = _nodeExtractionsToProcess.Pop();
        _extractionMessage.AddMarkupOrThrow(message);
        _extractionMessage.PushNewline();
        ExtractionResearchLabel.SetMessage(_extractionMessage);

        var delay = _nodeExtractionsToProcess.Count == 0
            ? ExtractNonEmptyShowDelaySpan
            : ExtractEmptyShowDelaySpan;
        _nextExtractStringTime = _timing.CurTime + delay;
        _extractionSum += value;
        ExtractionSumLabel.SetMarkup(Loc.GetString("analysis-console-extract-sum", ("value", _extractionSum)));

        if (_playerManager.LocalSession?.AttachedEntity is { } attachedEntity)
        {
            var volume = _nodeExtractionsToProcess.Count == 0 ? 1f : -10f;
            _audio.PlayGlobal(_owner.Comp.ScanFinishedSound, attachedEntity, AudioParams.Default.WithVolume(volume));
        }

        if (_nodeExtractionsToProcess.Count == 0)
            OnExtractButtonPressed?.Invoke();
    }

    public void Update(Entity<AnalysisConsoleComponent> ent)
    {
        _artifactAnalyzer.TryGetArtifactFromConsole(ent, out var arti);
        ArtifactView.SetEntity(arti);
        GraphControl.SetArtifact(arti);

        ExtractButton.Disabled = arti == null;

        if (arti == null)
            NoneSelectedLabel.Visible = false;

        NoArtiLabel.Visible = true;
        if (!_artifactAnalyzer.TryGetAnalyzer(ent, out _))
            NoArtiLabel.Text = Loc.GetString("analysis-console-info-no-scanner");
        else if (arti == null)
            NoArtiLabel.Text = Loc.GetString("analysis-console-info-no-artifact");
        else
            NoArtiLabel.Visible = false;

        if (_currentNode == null
            || arti == null
            || !_xenoArtifact.TryGetIndex((arti.Value, arti.Value), _currentNode.Value, out _))
            SetSelectedNode(null);
    }

    public void SetSelectedNode(Entity<XenoArtifactNodeComponent>? node)
    {
        InfoContainer.Visible = node != null;
        if (!_artifactAnalyzer.TryGetArtifactFromConsole(_owner, out var artifact))
            return;

        NoneSelectedLabel.Visible = node == null;

        if (node == null)
            return;

        var nodeId = _xenoArtifact.GetNodeId(node.Value);
        IDValueLabel.SetMarkup(Loc.GetString("analysis-console-info-id-value", ("id", nodeId)));

        // If active, state is 2. else, it is 0 or 1 based on whether it is unlocked, or not.
        int lockedState;
        if (_xenoArtifact.IsNodeActive(artifact.Value, node.Value))
            lockedState = 2;
        else
            lockedState = node.Value.Comp.Locked ? 0 : 1;

        LockedValueLabel.SetMarkup(Loc.GetString("analysis-console-info-locked-value",
            ("state", lockedState)));

        var percent = (float) node.Value.Comp.Durability / node.Value.Comp.MaxDurability;
        var color = percent switch
        {
            >= 0.75f => Color.Lime,
            >= 0.50f => Color.Yellow,
            _ => Color.Red
        };
        DurabilityValueLabel.SetMarkup(Loc.GetString("analysis-console-info-durability-value",
            ("color", color),
            ("current", node.Value.Comp.Durability),
            ("max", node.Value.Comp.MaxDurability)));

        var hasInfo = _xenoArtifact.HasUnlockedPredecessor(artifact.Value, node.Value);

        EffectValueLabel.SetMarkup(Loc.GetString("analysis-console-info-effect-value",
            ("state", hasInfo),
            ("info", _ent.GetComponentOrNull<MetaDataComponent>(node.Value)?.EntityDescription ?? string.Empty)));

        var predecessorNodes = _xenoArtifact.GetPredecessorNodes(artifact.Value.Owner, node.Value);
        if (!hasInfo)
        {
            TriggerValueLabel.SetMarkup(Loc.GetString("analysis-console-info-effect-value", ("state", false)));
        }
        else
        {
            var triggerStr = new StringBuilder();
            triggerStr.Append("- ");
            triggerStr.Append(Loc.GetString(node.Value.Comp.TriggerTip!));

            foreach (var predecessor in predecessorNodes)
            {
                triggerStr.AppendLine();
                triggerStr.Append("- ");
                triggerStr.Append(Loc.GetString(predecessor.Comp.TriggerTip!));
            }
            TriggerValueLabel.SetMarkup(Loc.GetString("analysis-console-info-triggered-value", ("triggers", triggerStr.ToString())));
        }

        ClassValueLabel.SetMarkup(Loc.GetString("analysis-console-info-class-value",
            ("class", Loc.GetString($"artifact-node-class-{Math.Min(6, predecessorNodes.Count + 1)}"))));
    }

    private void InitStaticLabels()
    {
        IDLabel.SetMarkup(Loc.GetString("analysis-console-info-id"));
        ClassLabel.SetMarkup(Loc.GetString("analysis-console-info-class"));
        LockedLabel.SetMarkup(Loc.GetString("analysis-console-info-locked"));
        EffectLabel.SetMarkup(Loc.GetString("analysis-console-info-effect"));
        TriggerLabel.SetMarkup(Loc.GetString("analysis-console-info-trigger"));
        DurabilityLabel.SetMarkup(Loc.GetString("analysis-console-info-durability"));
    }
}

