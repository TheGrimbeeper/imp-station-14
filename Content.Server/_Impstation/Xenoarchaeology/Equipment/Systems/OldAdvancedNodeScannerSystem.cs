using Content.Server.Xenoarchaeology.Equipment.Components;
using Content.Server.Xenoarchaeology.XenoArtifacts;
using Content.Server.Power.Components;
using Content.Shared.Construction.Components;


namespace Content.Server.Xenoarchaeology.Equipment.Systems;

public sealed class OldAdvancedNodeScannerSystem : EntitySystem
{

    /// <inheritdoc/>
    public override void Initialize()
    {
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
}