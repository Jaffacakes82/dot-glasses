using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Tests.DomainRejections;

namespace DotGlasses.Web.Tests.Catalogues;

/// <summary>Retiring a lens set through the Catalogues screen moves it to a separate retired list
/// with a Reactivate action; reactivating brings it back.</summary>
public class RetireLensSetScreenTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    [Fact]
    public async Task RetiringMovesTheLensSetToTheRetiredList_AndReactivatingBringsItBack()
    {
        var id = Guid.NewGuid();
        var name = $"Seasonal Readers {id:N}";
        factory.Seed(db => db.PresetCatalogues.Add(new PresetCatalogue { Id = id, Name = name, OwningOrgNodeId = OrganisationSeedConfiguration.DgiId }));

        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");

        var (_, afterRetire) = await AdminPortalFactory.PostAndFollowAsync(
            client, "/Catalogues/RetireCatalogue", AdminPortalFactory.Form(token, ("catalogueId", id.ToString())));

        var retiredSection = afterRetire[afterRetire.IndexOf("Retired lens sets", StringComparison.Ordinal)..];
        Assert.Contains(name, retiredSection);
        Assert.Contains("/Catalogues/ReactivateCatalogue", retiredSection);

        var (_, afterReactivate) = await AdminPortalFactory.PostAndFollowAsync(
            client, "/Catalogues/ReactivateCatalogue", AdminPortalFactory.Form(token, ("catalogueId", id.ToString())));

        var retiredIndex = afterReactivate.IndexOf("Retired lens sets", StringComparison.Ordinal);
        var activeSection = retiredIndex >= 0 ? afterReactivate[..retiredIndex] : afterReactivate;
        Assert.Contains(name, activeSection);
    }
}
