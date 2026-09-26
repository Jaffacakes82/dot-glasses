using System.Net;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Tests.DomainRejections;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests.Catalogues;

/// <summary>A lens set's name is the only thing a technician sees to tell lens sets apart
/// (ADR-0005), so two active ones may not share it — compared ignoring case. A retired lens set's
/// name is free again.</summary>
public class LensSetNameTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    private async Task<HttpResponseMessage> CreateAsync(HttpClient client, string name)
    {
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");
        return await client.PostAsync("/Catalogues/CreateCatalogue", AdminPortalFactory.Form(token, ("Name", name)));
    }

    [Fact]
    public async Task ASecondActiveLensSetWithTheSameNameInAnyCase_IsRefusedOnName()
    {
        var client = factory.CreateAdminClient(orgNodeId: OrganisationSeedConfiguration.DgiId);
        var name = $"Reading set {Guid.NewGuid():N}";

        Assert.Equal(HttpStatusCode.Found, (await CreateAsync(client, name)).StatusCode);

        var duplicate = await CreateAsync(client, name.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Contains("A lens set with this name already exists.", await duplicate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ABlankName_IsReportedAsRequired_NotChecked()
    {
        // The uniqueness check must not run on a name that isn't there.
        var client = factory.CreateAdminClient(orgNodeId: OrganisationSeedConfiguration.DgiId);

        var response = await CreateAsync(client, "");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("&#x27;Name&#x27; must not be empty.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ARetiredLensSetsNameCanBeUsedAgain()
    {
        var client = factory.CreateAdminClient(orgNodeId: OrganisationSeedConfiguration.DgiId);
        var name = $"Seasonal set {Guid.NewGuid():N}";
        Assert.Equal(HttpStatusCode.Found, (await CreateAsync(client, name)).StatusCode);

        Guid id;
        using (var scope = factory.Services.CreateScope())
        {
            id = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>().PresetCatalogues.Single(c => c.Name == name).Id;
        }

        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");
        await AdminPortalFactory.PostAndFollowAsync(client, "/Catalogues/RetireCatalogue", AdminPortalFactory.Form(token, ("catalogueId", id.ToString())));

        Assert.Equal(HttpStatusCode.Found, (await CreateAsync(client, name)).StatusCode);
    }
}
