namespace DotGlasses.Web.Auth;

/// <summary>
/// Dev placeholder values live in appsettings.Development.json. Staging/production source
/// Key/Issuer/Audience from Azure Key Vault (Phase 8, 2026-08-12) — see AppHost.cs's
/// IsPublishMode-gated Key Vault resource and Program.cs's conditional AddAzureKeyVaultSecrets
/// call. Secret names use Key Vault's "--" section-separator convention: Jwt--Key, Jwt--Issuer,
/// Jwt--Audience. Those three secrets must be set in the real Key Vault by the user (via `az
/// keyvault secret set` or the portal) after `azd up` provisions it — Claude cannot create them,
/// see CLAUDE.md's "no infra deployed from a developer machine" rule.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}
