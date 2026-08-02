using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

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
        optionsBuilder.UseNpgsql(LoadConnectionString());

        return new DotGlassesDbContext(optionsBuilder.Options, new HttpContextAccessor());
    }

    /// <summary>
    /// Located via [CallerFilePath] rather than Directory.GetCurrentDirectory() so this resolves
    /// correctly no matter where `dotnet ef` is invoked from — it always finds DotGlasses.Web's
    /// own appsettings relative to this source file's fixed position in the repo, not the
    /// process's working directory or build output layout.
    /// </summary>
    private static string LoadConnectionString([CallerFilePath] string sourceFilePath = "")
    {
        var webProjectDir = Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", "..", "DotGlasses.Web"));

        var configuration = new ConfigurationBuilder()
            .SetBasePath(webProjectDir)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetConnectionString("dotglassesdb")
            ?? throw new InvalidOperationException(
                "No ConnectionStrings:dotglassesdb found. Add one to " +
                "src/DotGlasses.Web/appsettings.Development.json before running `dotnet ef` commands.");
    }
}
