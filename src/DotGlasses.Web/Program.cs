using System.Security.Claims;
using System.Text;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using DotGlasses.Infrastructure;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Web.Auth;
using DotGlasses.Web.Authorization;
using DotGlasses.Web.Configuration;
using DotGlasses.Web.HostedServices;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.AddServiceDefaults();

// --- Persistence -----------------------------------------------------------------------
// "dotglassesdb" must match the database resource name AppHost gives Postgres.
builder.AddNpgsqlDbContext<DotGlassesDbContext>("dotglassesdb");

builder.Services.AddInfrastructure();

// --- Identity: cookie auth (MVC) + JWT bearer (API/App) --------------------------------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        // [OPEN] relaxed for local dev ergonomics; tighten before production.
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<DotGlassesDbContext>()
    .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<DevSeedOptions>(builder.Configuration.GetSection(DevSeedOptions.SectionName));
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddHostedService<RoleAndDevUserSeeder>();

builder.Services.AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });

// Configured via IOptions<JwtOptions>, resolved lazily when the auth handler is first used —
// not a value read from builder.Configuration up front — so config overrides applied after
// this point (e.g. WebApplicationFactory's test configuration, injected just before Build())
// still take effect. Reading JwtOptions into a local variable here would silently pin
// validation to whatever appsettings said before those later overrides landed.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptionsAccessor) =>
    {
        var jwtOptions = jwtOptionsAccessor.Value;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidateLifetime = true,
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name,
        };
    });

// --- RBAC (separate from the data-scoping query filter — see CLAUDE.md) ----------------
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.WidgetExampleCreate, policy =>
        policy.Requirements.Add(new MinimumRoleRequirement("Admin", "Manager")));
builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleAuthorizationHandler>();

// --- Validation --------------------------------------------------------------------------
builder.Services.AddValidatorsFromAssembly(typeof(DotGlasses.Contracts.WidgetExamples.WidgetExampleDto).Assembly);
builder.Services.AddFluentValidationAutoValidation();

// --- API versioning + Swagger ------------------------------------------------------------
builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();
builder.Services.AddSwaggerGen();

// [OPEN] dev-only origins for DotGlasses.App's standalone dev server (see its
// Properties/launchSettings.json). Replace with the real deployed App origin before production.
builder.Services.AddCors(options => options.AddPolicy("App", policy => policy
    .WithOrigins("https://localhost:7299", "http://localhost:5253")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // [OPEN] dev convenience only — production migration should be an explicit, reviewed step
    // (CI/CD pipeline or `dotnet ef database update`), not applied automatically on boot.
    // IsRelational() guards this for DotGlasses.Web.Tests, which swaps in the EF Core InMemory
    // provider (Migrate() isn't supported there, and isn't needed — InMemory has no schema).
    using var migrationScope = app.Services.CreateScope();
    var dbContext = migrationScope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
    if (dbContext.Database.IsRelational())
    {
        await dbContext.Database.MigrateAsync();
    }
}

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseCors("App");

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
        }
    });
}

app.MapControllers();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Exposed for DotGlasses.Web.Tests' WebApplicationFactory<Program>.
public partial class Program;
