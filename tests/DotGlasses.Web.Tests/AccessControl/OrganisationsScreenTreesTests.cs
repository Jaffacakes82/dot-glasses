using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests.AccessControl;

/// <summary>
/// Ticket 02: the Organisations screen builds one tree per separate part of the caller's scope
/// (ADR-0006, spec user stories 5-6), not one tree rooted at whichever node happens to come first.
/// Reuses AccessControlFixture's TwoCountriesAdmin (Kenya + Uganda — separate trees) and
/// DgiAndOutletAdmin (DGI + a retail point beneath it — a nested assignment that must not surface
/// as a second tree) rather than seeding new accounts, matching CombinedScopeTests' precedent.
/// </summary>
public class OrganisationsScreenTreesTests(AccessControlFixture fixture) : IClassFixture<AccessControlFixture>
{
    [Fact]
    public async Task AnAdminAssignedToTwoCountries_SeesBothCountriesAsSeparateTreesInLevelThenNameOrder()
    {
        var client = await fixture.SignInAsync(AccessControlFixture.TwoCountriesAdmin);

        var html = await client.GetStringAsync("/Organisations");

        // Both countries' own names, plus a node found only under each subtree, so this is
        // asserting on two whole trees being rendered, not just the two root labels.
        Assert.Contains("Kenya", html);
        Assert.Contains("Uganda", html);
        Assert.Contains("Kangemi Vision Centre", html); // under Kenya
        Assert.Contains("Kampala Outlet", html); // under Uganda

        // Each country is its own tree root — one row per id, not merged or duplicated.
        var kenyaLink = $"?selectedId={OrganisationSeedConfiguration.KenyaId}";
        var ugandaLink = $"?selectedId={fixture.SecondCountryId}";
        Assert.Equal(1, CountOccurrences(html, kenyaLink));
        Assert.Equal(1, CountOccurrences(html, ugandaLink));

        // Ordered: both are Country level, so alphabetical — Kenya's tree renders before Uganda's.
        var kenyaIndex = html.IndexOf(kenyaLink, StringComparison.Ordinal);
        var ugandaIndex = html.IndexOf(ugandaLink, StringComparison.Ordinal);
        Assert.True(kenyaIndex < ugandaIndex, "Expected Kenya's tree to render before Uganda's.");

        // Exactly one tree root per country: DGI (their common ancestor, outside both admins'
        // scope) never appears, so neither country's heading repeats as a second root.
        Assert.DoesNotContain("DOT Glasses International", html);
    }

    [Fact]
    public async Task AnAdminAssignedToDgiAndARetailPointBeneathIt_SeesOneDgiTreeNotTwo()
    {
        var client = await fixture.SignInAsync(AccessControlFixture.DgiAndOutletAdmin);

        var html = await client.GetStringAsync("/Organisations");

        Assert.Contains("DOT Glasses International", html);
        Assert.Contains("Kangemi Vision Centre", html);

        // Every node in the scoped set is rendered as exactly one tree-row link
        // (?selectedId=<id>) by _OrgTreeNode — unlike a plain text search, this can't be thrown
        // off by the selected node's name also appearing in the detail panel or a modal title.
        // DGI must be rendered exactly once as a tree row (one root, not two), and the nested
        // retail-point assignment must appear exactly once too, as a descendant of that one root.
        Assert.Equal(1, CountOccurrences(html, $"?selectedId={OrganisationSeedConfiguration.DgiId}"));
        Assert.Equal(1, CountOccurrences(html, $"?selectedId={OrganisationSeedConfiguration.KenyaRetailPointId}"));
    }

    [Fact]
    public async Task AnAdminAssignedToTwoCountries_SeesTheDeactivatedOrgsOfBoth_AndNoneOutsideThem()
    {
        // The deactivated list reads soft-deleted rows the global filter hides, so it applies the
        // scope by hand — and must use the whole scope, not one org out of it.
        var underKenya = SeedDeactivatedOrg(OrganisationSeedConfiguration.KenyaPath, OrganisationSeedConfiguration.KenyaId, OrganisationLevel.Intermediate);
        var underUganda = SeedDeactivatedOrg(AccessControlFixture.SecondCountryPath, fixture.SecondCountryId, OrganisationLevel.Intermediate);
        var outsideBoth = SeedDeactivatedOrg(OrganisationSeedConfiguration.DgiPath, OrganisationSeedConfiguration.DgiId, OrganisationLevel.Country);
        var client = await fixture.SignInAsync(AccessControlFixture.TwoCountriesAdmin);

        var html = await client.GetStringAsync("/Organisations");

        Assert.Contains(underKenya, html);
        Assert.Contains(underUganda, html);
        Assert.DoesNotContain(outsideBoth, html);
    }

    private string SeedDeactivatedOrg(string parentPath, Guid parentId, OrganisationLevel level)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        var name = $"Closed {Guid.NewGuid():N}";
        db.OrganisationNodes.Add(new OrganisationNode
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            Name = name,
            Level = level,
            HierarchyPath = $"{parentPath}{Random.Shared.Next(10_000_000, 99_999_999)}/",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            IsDeleted = true,
            DeletedAtUtc = DateTimeOffset.UtcNow,
        });
        db.SaveChanges();
        return name;
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
