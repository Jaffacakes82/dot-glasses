using System.Net;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Tests.DomainRejections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests.Catalogues;

/// <summary>Ticket 04 (spec user stories 20-21): creating a lens set chooses its owning org from
/// the admin's own Dgi/Country assignments, rather than reading the old single active org. The
/// field is offered only when more than one assignment qualifies; with exactly one it is chosen
/// automatically; a choice outside the admin's assignments is refused however it reaches the
/// server, whether or not the field was ever shown.</summary>
public class CreateLensSetOwningOrgTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    /// <summary>A second, unrelated Country — outside every fixture account's assignments — so a
    /// "choice outside the admin's assignments" test has a real Dgi/Country-level org to name.</summary>
    private Guid SeedSecondCountry()
    {
        var id = Guid.NewGuid();
        factory.Seed(db => db.OrganisationNodes.Add(new OrganisationNode
        {
            Id = id,
            ParentId = OrganisationSeedConfiguration.DgiId,
            Name = $"Uganda {id:N}",
            Level = OrganisationLevel.Country,
            HierarchyPath = $"/1/{Random.Shared.Next(1000, int.MaxValue)}/",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        }));
        return id;
    }

    private async Task<Guid> OwningOrgOfAsync(string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        return (await db.PresetCatalogues.SingleAsync(c => c.Name == name)).OwningOrgNodeId;
    }

    [Fact]
    public async Task WithMoreThanOneQualifyingAssignment_TheFieldIsShown_AndTheExplicitChoiceIsUsed()
    {
        var (client, _) = factory.CreateAdminClientWithAssignments(
            OrganisationSeedConfiguration.DgiId, OrganisationSeedConfiguration.KenyaId);

        var indexHtml = await client.GetStringAsync("/Catalogues");
        Assert.Contains("name=\"OwningOrgNodeId\"", indexHtml);
        Assert.Contains(OrganisationSeedConfiguration.KenyaId.ToString(), indexHtml);

        var name = $"DGI-or-Kenya set {Guid.NewGuid():N}";
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");
        var response = await client.PostAsync("/Catalogues/CreateCatalogue", AdminPortalFactory.Form(
            token, ("Name", name), ("OwningOrgNodeId", OrganisationSeedConfiguration.KenyaId.ToString())));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(OrganisationSeedConfiguration.KenyaId, await OwningOrgOfAsync(name));
    }

    [Fact]
    public async Task AChoiceOutsideTheAdminsDgiOrCountryAssignments_IsRefused()
    {
        var outsideCountryId = SeedSecondCountry();
        var (client, _) = factory.CreateAdminClientWithAssignments(
            OrganisationSeedConfiguration.DgiId, OrganisationSeedConfiguration.KenyaId);

        var name = $"Should not be created {Guid.NewGuid():N}";
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");
        var response = await client.PostAsync("/Catalogues/CreateCatalogue", AdminPortalFactory.Form(
            token, ("Name", name), ("OwningOrgNodeId", outsideCountryId.ToString())));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Choose which org this lens set belongs to.", await response.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        Assert.False(await db.PresetCatalogues.AnyAsync(c => c.Name == name));
    }

    [Fact]
    public async Task WithExactlyOneQualifyingAssignment_TheFieldIsHidden_AndItIsUsedAutomatically()
    {
        var client = factory.CreateAdminClient(OrganisationLevel.Country);

        var indexHtml = await client.GetStringAsync("/Catalogues");
        Assert.DoesNotContain("name=\"OwningOrgNodeId\"", indexHtml);

        var name = $"Kenya-only set {Guid.NewGuid():N}";
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");
        var response = await client.PostAsync("/Catalogues/CreateCatalogue", AdminPortalFactory.Form(token, ("Name", name)));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(OrganisationSeedConfiguration.KenyaId, await OwningOrgOfAsync(name));
    }
}
