using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` construct DotGlassesDbContext without going through
/// DotGlasses.Web's Program.cs — which needs a connection string injected by AppHost at
/// runtime and isn't available to the design-time CLI. Never used outside `dotnet ef`.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DotGlassesDbContext>
{
    public DotGlassesDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DotGlassesDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=dotglasses_designtime;Username=postgres;Password=postgres");

        return new DotGlassesDbContext(optionsBuilder.Options, new HttpContextAccessor());
    }
}
