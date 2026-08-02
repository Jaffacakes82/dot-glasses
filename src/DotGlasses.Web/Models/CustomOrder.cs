namespace DotGlasses.Web.Models;

public record CustomOrder(string Customer, string Outlet, string Prescription, string Status)
{
    public static readonly IReadOnlyDictionary<string, string> StatusColor = new Dictionary<string, string>
    {
        ["Submitted"] = "var(--dot-yellow)",
        ["In Lab"] = "var(--dot-blue)",
        ["Ready for Pickup"] = "var(--dot-pink)",
        ["Fulfilled"] = "var(--dot-green)",
    };

    private static readonly string[] Flow = ["Submitted", "In Lab", "Ready for Pickup", "Fulfilled"];

    public string? NextStatus => Flow.SkipWhile(s => s != Status).Skip(1).FirstOrDefault();
}
