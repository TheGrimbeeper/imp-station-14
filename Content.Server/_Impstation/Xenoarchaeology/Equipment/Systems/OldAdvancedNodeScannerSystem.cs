using Content.Server.Xenoarchaeology.Equipment.Components;
using Content.Server.Xenoarchaeology.XenoArtifacts;
using Content.Server.Power.Components;
using Content.Shared.Construction.Components;
using Content.Server.Xenoarchaeology.XenoArtifacts.Events;
using Content.Shared.Xenoarchaeology.Equipment.Components;
using System.Linq;

namespace Content.Server.Xenoarchaeology.Equipment.Systems;

public sealed class OldAdvancedNodeScannerSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly OldArtifactAnalyzerSystem _analyzer = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ArtifactComponent, ArtifactNodeEnteredEvent>(OnNodeEntered);
    }

    /// <summary>
    /// Is the advanced node scanner capable of scanning (anchored, powered, within range)
    /// </summary>
    /// <param name="advancedNodeScanner">The advanced node scanner</param>
    public bool CanProvideAdvancedScanning(Entity<OldAdvancedNodeScannerComponent> advancedNodeScanner)
    {
        // Must have a linked analyzer
        if (advancedNodeScanner.Comp.AnalyzerEntity is null)
            return false;

        // A.N.S and analyzer must both have transform components.
        if (!TryComp<TransformComponent>(advancedNodeScanner.Owner, out var advancedTransform)
        || !TryComp<TransformComponent>(advancedNodeScanner.Comp.AnalyzerEntity, out var analyzerTransform))
            return false;

        // A.N.S and analyzer must exist on the same map and other distance requirements.
        if (!advancedTransform.Coordinates.TryDistance(EntityManager, analyzerTransform.Coordinates, out var distance))
            return false;

        // If max distance is non-negative, then the distance between analyzer and A.N.S must be less than the max distance.
        if (advancedNodeScanner.Comp.MaxDistanceFromAnalyzerPad > 0 && advancedNodeScanner.Comp.MaxDistanceFromAnalyzerPad < distance)
            return false;

        // If A.N.S has anchorable component, then it must be anchored. (if no anchorable comp then its mapped or admeme and don't care)
        if (HasComp<AnchorableComponent>(advancedNodeScanner.Owner) && !advancedTransform.Anchored)
            return false;

        // If A.N.S has power, then it must be powered (if no power comp then who cares)
        if (TryComp<ApcPowerReceiverComponent>(advancedNodeScanner.Owner, out var power) && !power.Powered)
            return false;

        return true;
    }

    /// <summary>
    /// Catch every instance of the artifact activating and check if it is on a pad linked to advanced node scanner
    /// </summary>
    private void OnNodeEntered(EntityUid uid, ArtifactComponent component, ArtifactNodeEnteredEvent args)
    {
        // Get all pads within 2 tiles, check if they have the artifact on them
        if (!TryComp<TransformComponent>(uid, out var transform))
            return;

        var pads = _lookup.GetEntitiesInRange<OldArtifactAnalyzerComponent>(transform.Coordinates, 2);
        foreach (var pad in pads)
        {
            if (_analyzer.GetArtifactForAnalysis(pad) == uid)
            {
                TryAdvancedScanNodeId(pad);
                return;
            }
        }
    }

    /// <summary>
    /// Get and synchronize artifact ID if advanced node scanner is connected to an analyzer
    /// Also check if trigger/effect changed, (admin intervention) and blank those out
    /// </summary>
    public void TryAdvancedScanNodeId(EntityUid analyzer)
    {
        // Analyzer must have analyzer component
        if (!TryComp<OldArtifactAnalyzerComponent>(analyzer, out var analyzerComponent))
            return;

        // Can't advanced scan without advanced scanner
        var advancedNodeScanner = analyzerComponent.AdvancedNodeScanner;
        if (advancedNodeScanner is null)
            return;

        // advanced scanner needs advanced scan component
        if (!TryComp<OldAdvancedNodeScannerComponent>(advancedNodeScanner, out var ansComp))
            return; // if this happens something has gone wrong or admin intervention has occurred

        // needs to be plugged in and close to pad and such
        var ansEntity = new Entity<OldAdvancedNodeScannerComponent>((EntityUid)advancedNodeScanner, ansComp);
        if (CanProvideAdvancedScanning(ansEntity))
            return;

        // need an artifact to advanced scan
        var maybeArtifact = _analyzer.GetArtifactForAnalysis(analyzer);
        if (maybeArtifact is null)
            return;

        var artifact = (EntityUid)maybeArtifact;

        // artifact has to have artifact component
        if (!TryComp<ArtifactComponent>(artifact, out var artiComp))
            return; // if this happens then something has gone very wrong

        var currentNodeId = artiComp.CurrentNodeId;

        if (currentNodeId is null)
            return;

        //update existing artifact data
        if (ansComp.ScannedArtifactData.TryGetValue(artifact, out var scannedData))
        {
            scannedData.CurrentNodeId = (int)currentNodeId;
            scannedData.KnownNodeIds.Add((int)currentNodeId);

            //Get artifact trigger & effect; if already exist but different from actual node then admin intervention has happened and we
            // need to obfuscate them
            if (scannedData.Nodes.Exists(x => x.NodeId == currentNodeId))
            {
                var nodeData = scannedData.Nodes.Find(x => x.NodeId == currentNodeId);
                var artiNode = artiComp.NodeTree.Find(x => x.Id == currentNodeId);
                if (artiNode is not null)
                {
                    if (nodeData.Trigger != artiNode.Trigger || nodeData.Effect != artiNode.Effect)
                    {
                        nodeData.Trigger = "ERROR";
                        nodeData.Effect = "ERROR";
                    }
                    nodeData.Activated = artiNode.Triggered; //Advanced node scanner can tell if its triggered
                }
            }
        }
        else
        {
            // no existing data, so make a new data and put the current node in there
            var newKnownNodes = new HashSet<int>();
            newKnownNodes.Add((int)currentNodeId);
            var newData = new AdvancedNodeScannerArtifactData((int)currentNodeId, newKnownNodes, new List<AdvancedNodeScannerNodeData>());
            ansComp.ScannedArtifactData.Add(artifact, newData);
        }
        // Sync data to any attached analysis consoles
        var analyzerEntity = new Entity<OldArtifactAnalyzerComponent>((EntityUid)analyzer, analyzerComponent);
        TrySynchronizeAdvancedScanData(ansEntity, analyzerEntity);
    }

    /// <summary>
    /// Full scan of artifact
    /// </summary>
    public void TryAdvancedScanNodeFull(EntityUid analyzer)
    {
    // Analyzer must have analyzer component
        if (!TryComp<OldArtifactAnalyzerComponent>(analyzer, out var analyzerComponent))
            return;

        // Can't advanced scan without advanced scanner
        var advancedNodeScanner = analyzerComponent.AdvancedNodeScanner;
        if (advancedNodeScanner is null)
            return;

        // advanced scanner needs advanced scan component
        if (!TryComp<OldAdvancedNodeScannerComponent>(advancedNodeScanner, out var ansComp))
            return; // if this happens something has gone wrong or admin intervention has occurred

        // needs to be plugged in and close to pad and such
        var ansEntity = new Entity<OldAdvancedNodeScannerComponent>((EntityUid)advancedNodeScanner, ansComp);
        if (CanProvideAdvancedScanning(ansEntity))
            return;

        // need an artifact to advanced scan
        var maybeArtifact = _analyzer.GetArtifactForAnalysis(analyzer);
        if (maybeArtifact is null)
            return;

        var artifact = (EntityUid)maybeArtifact;

        // artifact has to have artifact component
        if (!TryComp<ArtifactComponent>(artifact, out var artiComp))
            return; // if this happens then something has gone very wrong

        var currentNodeId = artiComp.CurrentNodeId;

        if (currentNodeId is null)
            return;

        // artifact must have an actual node in the tree, otherwise WTF?
        if (!artiComp.NodeTree.Exists(x => x.Id == currentNodeId))
            return;

        var artiNode = artiComp.NodeTree.Find(x => x.Id == currentNodeId);

        var artiNodeParent = GetParentOfNode(artiComp, artiNode);
        var artiNodeChildren = artiNode.Edges.ToList<int>();
        artiNodeChildren.Remove(artiNodeParent);

        //update existing artifact data
        if (ansComp.ScannedArtifactData.TryGetValue(artifact, out var scannedData))
        {
            scannedData.CurrentNodeId = (int)currentNodeId;
            scannedData.KnownNodeIds.Add((int)currentNodeId);

            // if we don't have info yet, we need to add it. otherwise we assume we already know.
            if (!scannedData.Nodes.Exists(x => x.NodeId == currentNodeId))
            {
                scannedData.Nodes.Add(new AdvancedNodeScannerNodeData(
                    (int)currentNodeId,
                    artiNode.Depth,
                    artiNodeParent,
                    artiNodeChildren,
                    artiNode.Trigger,
                    artiNode.Effect,
                    artiNode.Triggered
                ));
            }
        }
        else
        {
            // no existing data, so make a new data and put the current node in there
            var newKnownNodes = new HashSet<int>((int)currentNodeId);
            var newData = new AdvancedNodeScannerArtifactData((int)currentNodeId, newKnownNodes, new List<AdvancedNodeScannerNodeData>());

            //We scan the entire node
            var scannedNode = new AdvancedNodeScannerNodeData(
                (int)currentNodeId,
                artiNode.Depth,
                artiNodeParent,
                artiNodeChildren,
                artiNode.Trigger,
                artiNode.Effect,
                artiNode.Triggered
            );

            newData.Nodes.Add(scannedNode);
            ansComp.ScannedArtifactData.Add(artifact, newData);
        }
        // Sync data to any attached analysis consoles
        var analyzerEntity = new Entity<OldArtifactAnalyzerComponent>((EntityUid)analyzer, analyzerComponent);
        TrySynchronizeAdvancedScanData(ansEntity, analyzerEntity);
    }

    /// <summary>
    /// Figure out what the parent of a node is (its the edge of the node with the lowest depth)
    /// </summary>
    private static int GetParentOfNode(ArtifactComponent comp, ArtifactNode node)
    {
        // TODO!!
        return 0;
    }

    /// <summary>
    /// Synchronise advanced scan data from advanced node scanner to analysis console. Requires both advanced node scanner and analyzer
    /// </summary>
    public static void TrySynchronizeAdvancedScanData(Entity<OldAdvancedNodeScannerComponent> ansEntity, Entity<OldArtifactAnalyzerComponent> analyzerEntity)
    {
        //TODO
        return;
    }

    /// <summary>
    /// Synchronise advanced scan data from advanced node scanner to analysis console. Takes in Advanced Node Scanner as argument.
    /// </summary>
    public static void TrySynchronizeAdvancedScanData(Entity<OldAdvancedNodeScannerComponent> ansEntity)
    {
        //TODO get analyzer entity from ansEntity, call full TrySync
        return;
    }

    /// <summary>
    /// Synchronise advanced scan data from advanced node scanner to analysis console. Takes in Artifact Analyzer as argument.
    /// </summary>
    public static void TrySynchronizeAdvancedScanData(Entity<OldArtifactAnalyzerComponent> analyzerEntity)
    {
        //TODO get ansEntity from analyzerEntity, call full TrySync
        return;
    }

}