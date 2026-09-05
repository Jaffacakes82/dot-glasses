using System.Data.Common;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Sales;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Tests.Postgres;
using DotGlasses.Rules;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ContractFrameCoverage = DotGlasses.Contracts.Sales.FrameCoverage;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// What validating a consultation actually costs in database round trips — the claim ADR-0002
/// makes and ticket 12 had to deliver, measured rather than asserted in prose.
///
/// Before the snapshot, a preset-range Sale cost 7 + 3n + n(n−1)/2 sequential reference-data
/// lookups (14 at two coatings, 19 at three) because each referenced field was checked with its
/// own query. The snapshot replaces all of them with one load, memoized for the scope, after which
/// every rule is answered in memory: DotGlasses.Rules references only DotGlasses.Contracts and so
/// structurally cannot issue a query.
///
/// Run against real Postgres and counted at the DbCommand boundary, so what is measured is the
/// round trips actually made, not what the code appears to make.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ReferenceDataSnapshotProviderTests(PostgresContainerFixture postgres)
{
    /// <summary>Counts every command EF executes, whatever the shape of the query.</summary>
    private sealed class CommandCountingInterceptor : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task ValidatingASale_CostsTheSameOneLoadHoweverManyFieldsItReferences()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var counter = new CommandCountingInterceptor();

        await using var context = PostgresContainerFixture.CreateContext(connectionString, null, counter);
        var provider = new ReferenceDataSnapshotProvider(context);

        var snapshot = await provider.GetAsync();
        var afterLoad = counter.Count;

        // The load itself is a fixed handful of set-based reads — items, catalogues, lens options,
        // coating availability, pairings, exclusions — and nothing about it varies with the
        // request being checked.
        Assert.Equal(6, afterLoad);

        // A Sale referencing as much reference data as one can: an occupation, a referral reason,
        // a frame colour, a hard-case colour, a lens type and three coatings. Every id is unknown
        // to the seeded library, so each is genuinely looked up and rejected rather than
        // short-circuited.
        var request = new CreateSaleRequest
        {
            Id = Guid.NewGuid(),
            FullName = "Amina Okoro",
            Gender = Gender.Female,
            LensRangeType = LensRangeType.Custom,
            CustomSphereLeft = 1.00m,
            CustomSphereRight = -0.50m,
            CustomAddPowerLeft = 1.00m,
            LensTypeRefId = Guid.NewGuid(),
            PupilDistanceMm = 62m,
            OccupationRefId = Guid.NewGuid(),
            ReferredOrTreated = true,
            ReferralReasonRefId = Guid.NewGuid(),
            ReferralLocationFreeText = "Kisumu clinic",
            FrameColourRefId = Guid.NewGuid(),
            FrameCoverage = ContractFrameCoverage.FullFrame,
            HardCaseSold = true,
            HardCaseColourRefId = Guid.NewGuid(),
            CoatingRefIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
        };

        var result = ConsultationRules.Check(request, snapshot);

        // It failed on plenty of fields — so the rules really did run — and cost nothing to do so.
        Assert.False(result.IsValid);
        Assert.Equal(afterLoad, counter.Count);
    }

    [Fact]
    public async Task TwoReadsInOneScope_ShareTheOneLoad()
    {
        // What makes "once per request" true: the provider is registered scoped, and a second
        // caller inside the same request — the Admin Portal's lead-conversion screen reads the
        // snapshot for its lens summary as well as for its rules — gets the copy already loaded.
        var connectionString = await postgres.CreateDatabaseAsync();
        var counter = new CommandCountingInterceptor();

        await using var context = PostgresContainerFixture.CreateContext(connectionString, null, counter);
        var provider = new ReferenceDataSnapshotProvider(context);

        var first = await provider.GetAsync();
        var afterFirst = counter.Count;
        var second = await provider.GetAsync();

        Assert.Same(first, second);
        Assert.Equal(afterFirst, counter.Count);
    }
}
