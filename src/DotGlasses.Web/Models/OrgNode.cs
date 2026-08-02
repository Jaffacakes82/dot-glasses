namespace DotGlasses.Web.Models;

/// <summary>
/// Placeholder shape for the Organisations tree — the real org hierarchy entity isn't designed
/// yet (see CLAUDE.md), but the tree is known to be arbitrary-depth (DGI root, RetailPoint
/// leaves, any number of Retailer/Reseller layers between), not a fixed 3-tier structure. Don't
/// flatten that assumption away when the real entity lands.
/// </summary>
public record OrgNode(string Id, string Name, string Type, string? Catalogue, string? Kind, IReadOnlyList<OrgNode> Children)
{
    public static readonly IReadOnlyDictionary<string, string> TypeColor = new Dictionary<string, string>
    {
        ["DGI"] = "var(--dot-black)",
        ["Country"] = "var(--dot-blue)",
        ["Retailer"] = "var(--dot-orange)",
        ["RetailPoint"] = "var(--dot-green)",
    };
}
