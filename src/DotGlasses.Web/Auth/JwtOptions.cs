namespace DotGlasses.Web.Auth;

/// <summary>
/// [OPEN] Dev placeholder values live in appsettings.Development.json. Production must source
/// Key/Issuer/Audience from a real secret store (Azure Key Vault / App Configuration) — not
/// committed to source — before this ships.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}
