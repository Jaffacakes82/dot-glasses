using DotGlasses.Application.Common;
using DotGlasses.Application.Customers;
using DotGlasses.Application.CustomOrders;
using DotGlasses.Application.Dashboard;
using DotGlasses.Application.Leads;
using DotGlasses.Application.Notifications;
using DotGlasses.Application.Organisations;
using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.Reporting;
using DotGlasses.Application.Sales;
using DotGlasses.Application.Users;
using DotGlasses.Application.VisionTests;
using DotGlasses.Application.WidgetExamples;
using Azure.Communication.Email;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Infrastructure.Notifications;
using DotGlasses.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotGlasses.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();

        // AuditSaveChangesInterceptor is NOT registered as IInterceptor here for DI
        // auto-discovery — that doesn't actually fire for a context resolved via Aspire's pooled
        // AddNpgsqlDbContext (found live: CreatedAtUtc/CreatedBy silently never got stamped
        // through the real HTTP pipeline). It's wired instead in DotGlasses.Web's Program.cs, via
        // AddNpgsqlDbContext's configureDbContextOptions callback — DotGlassesDbContext can't
        // override OnConfiguring itself, EF Core throws at startup for that on a pooled context.
        // See CLAUDE.md's Test/Lead/Sale API section for the full story.

        services.AddScoped<IWidgetExampleRepository, WidgetExampleRepository>();
        services.AddScoped<IWidgetExampleService, WidgetExampleService>();
        services.AddScoped<IUnscopedReportQueryService, UnscopedReportQueryService>();
        services.AddScoped<IEventHistoryQueryService, EventHistoryQueryService>();
        services.AddScoped<ICustomOrderService, CustomOrderService>();
        services.AddScoped<IDashboardQueryService, DashboardQueryService>();
        services.AddEmailSender();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IUserOrgAssignmentService, UserOrgAssignmentService>();

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<DotGlassesDbContext>());
        services.AddScoped<IReferenceDataLookupService, ReferenceDataLookupService>();
        services.AddScoped<IReferenceDataQueryService, ReferenceDataQueryService>();
        services.AddScoped<IReferenceDataAdminService, ReferenceDataAdminService>();
        services.AddScoped<IOrganisationAdminService, OrganisationAdminService>();
        services.AddScoped<IPresetCatalogueQueryService, PresetCatalogueQueryService>();
        services.AddScoped<IPresetCatalogueAdminService, PresetCatalogueAdminService>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();

        services.AddScoped<IVisionTestRepository, TestRepository>();
        services.AddScoped<IVisionTestService, VisionTestService>();

        services.AddScoped<ILeadRepository, LeadRepository>();
        services.AddScoped<ILeadService, LeadService>();

        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<ISaleService, SaleService>();

        return services;
    }

    /// <summary>ACS_CONNECTION_STRING/ACS_SENDER_DOMAIN only ever exist in configuration when
    /// AppHost.cs's IsPublishMode-gated acs.bicep resource actually provisioned and injected them
    /// (see AzureEmailSender's own doc comment) — local `dotnet run` never sets them, so this
    /// always falls back to LoggingEmailSender in dev without any extra configuration needed.</summary>
    private static void AddEmailSender(this IServiceCollection services)
    {
        services.AddScoped<IEmailSender>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var connectionString = configuration["ACS_CONNECTION_STRING"];
            var senderDomain = configuration["ACS_SENDER_DOMAIN"];

            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(senderDomain))
            {
                return new LoggingEmailSender(sp.GetRequiredService<ILogger<LoggingEmailSender>>());
            }

            var emailClient = new EmailClient(connectionString);
            var senderAddress = $"DoNotReply@{senderDomain}";
            return new AzureEmailSender(emailClient, senderAddress, sp.GetRequiredService<ILogger<AzureEmailSender>>());
        });
    }
}
