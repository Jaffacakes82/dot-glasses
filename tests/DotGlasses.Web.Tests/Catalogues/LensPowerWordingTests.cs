using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Tests.DomainRejections;

namespace DotGlasses.Web.Tests.Catalogues;

/// <summary>
/// The Admin Portal says "lens power" and never "lens strength" (ADR-0007; CONTEXT.md). A lens is a
/// lens power now — the "Lens strength" reference list is retired — so the old word must not survive
/// in any screen, dialog error or script the portal serves. The Field App has no test project;
/// its copy is held to the same rule by the source sweep noted in the ticket, and the server-side
/// validation messages both apps show are covered by the Rules tests.
/// </summary>
public class LensPowerWordingTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    private static void AssertNoLensStrength(string text, string where) =>
        Assert.False(
            text.Contains("strength", StringComparison.OrdinalIgnoreCase),
            $"{where} still says \"strength\" — a lens is a lens power now.");

    [Theory]
    [InlineData("/Catalogues")]
    [InlineData("/Catalogues/LensPowers")]
    [InlineData("/ReferenceData")]
    public async Task TheScreen_NeverSaysLensStrength(string path)
    {
        var html = await factory.CreateAdminClient().GetStringAsync(path);

        AssertNoLensStrength(html, path);
    }

    [Fact]
    public async Task TheLensSetsScreen_SaysLensPower()
    {
        var html = await factory.CreateAdminClient().GetStringAsync("/Catalogues");

        Assert.Contains("Lens powers", html);
    }

    [Fact]
    public async Task TheLeadConversionScreen_NeverSaysLensStrength()
    {
        var customerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        factory.Seed(db =>
        {
            db.Customers.Add(new Customer { Id = customerId, FullName = "Wanjiru Kamau", PhoneNumber = "+254711000000", HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath });
            db.Leads.Add(new Lead { Id = leadId, CustomerId = customerId, TechnicianUserId = Guid.NewGuid(), HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath, ConsentGiven = true });
        });

        var html = await factory.CreateAdminClient().GetStringAsync($"/Leads/Convert/{leadId}");

        AssertNoLensStrength(html, "the Lead conversion screen");
    }

    [Fact]
    public async Task ARefusedLens_NamesItsFieldsAsLensPowers()
    {
        var lensSetId = Guid.NewGuid();
        factory.Seed(db => db.PresetCatalogues.Add(new PresetCatalogue { Id = lensSetId, Name = $"Wording {lensSetId:N}", OwningOrgNodeId = OrganisationSeedConfiguration.DgiId }));
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");

        // Every check the dialog makes fails at once: no label, a power outside the allowed values,
        // an add with no lens type, no coatings.
        var response = await client.PostAsync("/Catalogues/SaveLens", AdminPortalFactory.Form(token,
            ("CatalogueId", lensSetId.ToString()),
            ("Label", ""),
            ("Sphere", "10.25"),
            ("Cylinder", "-1.00"),
            ("Add", "2.00")));

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("must be between", html);
        AssertNoLensStrength(html, "the refused Add lens dialog");
    }

    [Theory]
    [InlineData("/js/lens-dialog.js")]
    [InlineData("/js/lead-conversion.js")]
    public async Task TheScripts_NeverSayLensStrength(string path)
    {
        var script = await factory.CreateAdminClient().GetStringAsync(path);

        AssertNoLensStrength(script, path);
    }
}
