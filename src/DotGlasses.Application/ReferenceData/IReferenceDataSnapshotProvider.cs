using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Application.ReferenceData;

/// <summary>
/// The server-side half of ADR-0002's two adapters: fills a <see cref="ReferenceDataSnapshot"/>
/// from the database, retired items included, so a historical Test/Lead/Sale still renders the
/// label of an option that has since been retired.
///
/// Registered scoped and loaded at most once per request. It is deliberately <em>not</em> cached
/// across requests: DotGlasses.Web runs on Container Apps and can scale to multiple replicas, so
/// an in-memory cache would be per-replica and an admin's reference-data edit would be live on one
/// replica and stale on the others until an invalidation crossed them. See ADR-0002.
/// </summary>
public interface IReferenceDataSnapshotProvider
{
    Task<ReferenceDataSnapshot> GetAsync(CancellationToken cancellationToken = default);
}
