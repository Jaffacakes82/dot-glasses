using System.Text.RegularExpressions;
using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Tests.DomainRejections;

namespace DotGlasses.Web.Tests.Catalogues;

/// <summary>Ticket 08: assigning a lens set to an org shows a success message after the
/// POST-redirect-GET, the same TempData pattern the rest of the Admin Portal already uses
/// (Account/Login, Account/Settings, LeadConversion).</summary>
public class AssignCatalogueConfirmationTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    [Fact]
    public async Task AssigningALensSet_ShowsASuccessMessage_AfterTheRedirect()
    {
        var catalogueId = SeedActiveLensSet();
        var client = factory.CreateAdminClient();

        // Plain page copy already says "Assign lens sets to a retailer" and "Assigned to N
        // org(s)" — proving there's a real confirmation, not just those, means asserting on the
        // TempData banner itself rather than the word "assigned" anywhere on the page. A plain
        // GET first is the negative control: the banner must not be there before anything was
        // assigned.
        var beforeHtml = await client.GetStringAsync("/Catalogues");
        Assert.Null(SuccessBanner(beforeHtml));

        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");
        var (_, landingHtml) = await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/AssignCatalogues",
            AdminPortalFactory.Form(token,
                ("OrgNodeId", OrganisationSeedConfiguration.KenyaRetailPointId.ToString()),
                ("CatalogueIds", catalogueId.ToString())));

        var banner = SuccessBanner(landingHtml);
        Assert.NotNull(banner);
        Assert.Contains("assigned", banner, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The inner text of the page's <c>alert-success</c> banner, or null if none is
    /// rendered — the same style block Account/Login and Account/Settings use for TempData
    /// ["Info"].</summary>
    private static string? SuccessBanner(string html)
    {
        var match = Regex.Match(html, "alert-success[^>]*>(.*?)</div>", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private Guid SeedActiveLensSet()
    {
        var id = Guid.NewGuid();
        factory.Seed(db => db.PresetCatalogues.Add(new PresetCatalogue
        {
            Id = id,
            Name = $"Lens set {id:N}",
            OwningOrgNodeId = OrganisationSeedConfiguration.DgiId,
        }));
        return id;
    }
}
