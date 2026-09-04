using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.App.ReferenceData;

/// <summary>
/// The Field App half of ADR-0002's two adapters. Builds the snapshot the shared rules take from
/// the reference data this device already holds — the live response when the technician is online,
/// the IndexedDB copy when they are not — so a pre-submit check works with no connectivity and
/// needs no API change. The server's own adapter fills the same type from the database.
/// </summary>
public static class ReferenceDataSnapshotAdapter
{
    public static ReferenceDataSnapshot ToSnapshot(this IReferenceDataClient client) =>
        ReferenceDataSnapshot.FromCachedReferenceData(client.AllItems, client.Catalogues, client.CoatingPairings, client.CoatingExclusions);
}
