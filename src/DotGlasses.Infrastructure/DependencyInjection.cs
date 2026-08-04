using DotGlasses.Application.Common;
using DotGlasses.Application.Customers;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.Reporting;
using DotGlasses.Application.VisionTests;
using DotGlasses.Application.WidgetExamples;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();

        // Registered as IInterceptor (not wired via AddInterceptors in Program.cs): EF Core
        // auto-discovers interceptors registered in the app's DI container for any DbContext
        // resolved through DI, at the DbContext's own lifetime/scope — the correct fix for an
        // interceptor that itself depends on the scoped ICurrentUserContext. Registering it
        // singleton and wiring it manually in AddDbContext's options callback would create a
        // captive dependency that pins the interceptor to whichever request scope built it first.
        services.AddScoped<IInterceptor, AuditSaveChangesInterceptor>();

        services.AddScoped<IWidgetExampleRepository, WidgetExampleRepository>();
        services.AddScoped<IWidgetExampleService, WidgetExampleService>();
        services.AddScoped<IUnscopedReportQueryService, UnscopedReportQueryService>();

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<DotGlassesDbContext>());
        services.AddScoped<IReferenceDataLookupService, ReferenceDataLookupService>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();

        services.AddScoped<IVisionTestRepository, TestRepository>();
        services.AddScoped<IVisionTestService, VisionTestService>();

        return services;
    }
}
