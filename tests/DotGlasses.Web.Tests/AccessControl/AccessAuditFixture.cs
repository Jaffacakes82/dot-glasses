using System.Net.Http.Headers;
using System.Security.Claims;
using DotGlasses.Application.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests.AccessControl;

/// <summary>
/// AccessControlFixture plus what the access audit needs on top: real hierarchy-scoped rows to
/// <em>not</em> see, and the three kinds of caller the audit is about.
///
/// The rows matter as much as the callers. "A user with no assignment sees nothing" is only
/// evidence when there is something to see, so a Customer, Test, Lead and custom-order Sale are
/// recorded at the seeded Kenya retail point ("own" — the audit's retail-point user is assigned
/// there) and again at a sibling outlet under the same reseller ("foreign"). The sibling sits at
/// "/1/2/3/41/", which shares the characters "/1/2/3/4" with the retail point at "/1/2/3/4/" and
/// is kept out of its scope only by the trailing slash — the same landmine AccessControlFixture
/// plants one level up.
/// </summary>
public class AccessAuditFixture : AccessControlFixture
{
    public const string SiblingOutletPath = "/1/2/3/41/";
    public const string SiblingOutletName = "Audit Sibling Outlet";
    public const string DeactivatedOutletPath = "/1/2/3/42/";
    public const string DeactivatedOutletName = "Audit Deactivated Outlet";

    public const string OwnCustomerName = "Audit Own Customer";
    public const string ForeignCustomerName = "Audit Foreign Customer";
    public const string CustomerPhone = "0700999888";

    /// <summary>The seeded Kenya retail point's name up to its em dash, which Razor encodes.</summary>
    public const string OwnOutletNameFragment = "Outreach Post";

    public const string OutletTargetUser = "outlet-target@test.local";

    public sealed record Records(Guid CustomerId, Guid TestId, Guid LeadId, Guid SaleId, Guid OrderId);

    public sealed record Account(string UserName, Guid UserId);

    public Guid SiblingOutletId { get; private set; }

    public Guid DeactivatedOutletId { get; private set; }

    /// <summary>Recorded at the seeded Kenya retail point.</summary>
    public Records Own { get; private set; } = null!;

    /// <summary>Recorded at the sibling outlet.</summary>
    public Records Foreign { get; private set; } = null!;

    /// <summary>(c): role User, assigned to the Kenya retail point alone.</summary>
    public Account OutletUser { get; private set; } = null!;

    /// <summary>(b): role User, assigned to the Kenya reseller alone — above every retail point,
    /// so with no eligible recording location.</summary>
    public Account ResellerUser { get; private set; } = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();

            var sibling = Outlet(SiblingOutletName, SiblingOutletPath, deactivated: false);
            var deactivated = Outlet(DeactivatedOutletName, DeactivatedOutletPath, deactivated: true);
            db.OrganisationNodes.AddRange(sibling, deactivated);
            SiblingOutletId = sibling.Id;
            DeactivatedOutletId = deactivated.Id;

            Own = Record(db, OrganisationSeedConfiguration.KenyaRetailPointPath, OwnCustomerName);
            Foreign = Record(db, SiblingOutletPath, ForeignCustomerName);
            await db.SaveChangesAsync();
        }

        OutletUser = await NewAccountAsync(RoleNames.User, OrganisationSeedConfiguration.KenyaRetailPointId);
        ResellerUser = await NewAccountAsync(RoleNames.User, OrganisationSeedConfiguration.KenyaRetailerId);
    }

    public async Task<Account> NewAccountAsync(string role, params Guid[] assignedOrgIds)
    {
        var (userName, userId) = await CreateAccountAsync(role, assignedOrgIds);
        return new Account(userName, userId);
    }

    /// <summary>(a): an account holding no org assignment at all. The service layer refuses to
    /// produce one (removing a user's last assignment is a DomainRuleViolationException), so the
    /// state is made the way it arises in practice — the assignment rows go, behind the
    /// application's back, as a database clear-down would take them.</summary>
    public async Task<Account> NewAccountWithNoAssignmentAsync(string role)
    {
        var account = await NewAccountAsync(role, OrganisationSeedConfiguration.KenyaRetailPointId);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        db.UserOrgAssignments.RemoveRange(db.UserOrgAssignments.Where(a => a.UserId == account.UserId));
        await db.SaveChangesAsync();
        Assert.False(await db.UserOrgAssignments.AnyAsync(a => a.UserId == account.UserId));

        return account;
    }

    /// <summary>A Field App client for <paramref name="account"/> holding a JWT built directly, so
    /// its current-location claim can name anything — nothing, or an org the account could never
    /// have been issued a token for. That is how a stale or tampered device token reaches the
    /// server; /api/v1/auth/login itself only ever issues a valid location or none.</summary>
    public HttpClient ApiClient(Account account, Guid? currentLocationId)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, account.UserId.ToString()),
            new(ClaimTypes.Name, account.UserName),
        ];
        if (currentLocationId is { } locationId)
        {
            claims.Add(new(DotGlassesClaimTypes.CurrentLocationId, locationId.ToString()));
        }

        var (token, _) = Factory.Services.GetRequiredService<IJwtTokenService>().CreateToken(claims);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<T> ReadAsync<T>(Func<DotGlassesDbContext, Task<T>> read)
    {
        using var scope = Factory.Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>());
    }

    private static OrganisationNode Outlet(string name, string path, bool deactivated) => new()
    {
        Id = Guid.NewGuid(),
        ParentId = OrganisationSeedConfiguration.KenyaRetailerId,
        Name = name,
        Level = OrganisationLevel.RetailPoint,
        HierarchyPath = path,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        IsDeleted = deactivated,
    };

    /// <summary>One of each hierarchy-scoped record at <paramref name="hierarchyPath"/>: a
    /// Customer, a Test, an open Lead and a custom-order Sale awaiting fulfilment (so it would
    /// show on Custom Orders as well as Event History).</summary>
    private static Records Record(DotGlassesDbContext db, string hierarchyPath, string customerName)
    {
        var now = DateTimeOffset.UtcNow;
        var customer = new Customer { Id = Guid.NewGuid(), HierarchyPath = hierarchyPath, FullName = customerName, PhoneNumber = CustomerPhone, CreatedAtUtc = now };
        var test = new Test { Id = Guid.NewGuid(), HierarchyPath = hierarchyPath, TechnicianUserId = Guid.NewGuid(), CreatedAtUtc = now };
        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            HierarchyPath = hierarchyPath,
            TechnicianUserId = Guid.NewGuid(),
            CustomerId = customer.Id,
            ConsentGiven = true,
            ReasonNotPurchasedRefId = db.ReferenceDataItems.First(x => x.Category == ReferenceDataCategory.ReasonNotPurchased && x.IsActive).Id,
            CustomerToldPrice = true,
            CreatedAtUtc = now,
        };
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            HierarchyPath = hierarchyPath,
            TechnicianUserId = Guid.NewGuid(),
            CustomerId = customer.Id,
            ConsentGiven = true,
            LensRangeType = LensRangeType.Custom,
            CreatedAtUtc = now,
        };
        var order = new CustomOrder
        {
            Id = Guid.NewGuid(),
            HierarchyPath = hierarchyPath,
            Status = FulfilmentStatus.Submitted,
            PlacedAtUtc = now,
            SaleId = sale.Id,
            CreatedAtUtc = now,
        };

        db.Customers.Add(customer);
        db.Tests.Add(test);
        db.Leads.Add(lead);
        db.Sales.Add(sale);
        db.CustomOrders.Add(order);
        return new Records(customer.Id, test.Id, lead.Id, sale.Id, order.Id);
    }
}
