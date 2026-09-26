using DotGlasses.Web.Tests.DomainRejections;

namespace DotGlasses.Web.Tests.Catalogues;

/// <summary>The screen speaks CONTEXT.md's language — a <b>lens set</b>, never a "package" — and
/// no longer asks for the free-text "Diopter / strength range", which nothing read and the device
/// never saw (ADR-0005).</summary>
public class CataloguesScreenWordingTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    [Fact]
    public async Task TheScreenSaysLensSet_AndNoLongerAsksForADiopterRange()
    {
        var html = await factory.CreateAdminClient().GetStringAsync("/Catalogues");

        Assert.Contains("Create lens set", html);
        Assert.Contains("Assign lens sets to a retailer", html);
        Assert.DoesNotContain("package", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Diopter", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"RangeDescription\"", html);
    }
}
