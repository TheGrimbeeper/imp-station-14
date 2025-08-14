namespace Content.Server.Xenoarchaeology.Equipment.Components;

[RegisterComponent]
public sealed partial class OldAdvancedNodeScannerComponent : Component
{
    /// <summary>
    /// The analyzer entity the advanced node scanner is linked.
    /// Can be null if not linked.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? AnalyzerEntity;

    /// <summary>
    /// Stored scanned artifacts and their nodes
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<EntityUid, AdvancedNodeScannerNodeData> ScannedArtifactData = new Dictionary<EntityUid, AdvancedNodeScannerNodeData>();
}


[Serializable]
public struct AdvancedNodeScannerNodeData(
    int nodeId,
    int depth,
    int parentId,
    List<int> childIds,
    string trigger,
    string effect,
    bool activated)
{
    /// <summary>
    /// stored data about an artifact node
    /// </summary>
    public int NodeId = nodeId;

    public int Depth = depth;

    public int ParentId = parentId;

    public List<int> ChildIds = childIds;

    public string Trigger = trigger;

    public string Effect = effect;

    public bool Activated = activated;
}