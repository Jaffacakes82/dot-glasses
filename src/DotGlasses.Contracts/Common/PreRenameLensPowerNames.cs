using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Contracts.Tests;

namespace DotGlasses.Contracts.Common;

/// <summary>
/// Lets the three create requests still read the per-eye lens power fields under their names
/// from before lens-power ticket 01 (<c>customSphereLeft</c> … <c>customAddPowerRight</c>).
///
/// Why it exists: the Field App's outbox stores each request as JSON and posts that JSON as-is
/// when it syncs (SyncService), and a device can hold queued records across a release — or still
/// be running the previous cached build for a visit. System.Text.Json ignores names it doesn't
/// know, so without this a queued Custom prescription would arrive with no spheres and be refused,
/// and reloading it on Failed records would show blank lens fields: the prescription would be lost
/// with nothing on the device to recover it from.
///
/// Read-only aliases: an old name fills the new property only when the new one is empty, and is
/// never written back out. Registered on the API's JSON options (Web's Program.cs) and on the Field
/// App's Failed-record reload. Delete it once no device can still hold a pre-rename payload.
/// </summary>
public static class PreRenameLensPowerNames
{
    private static readonly (string Legacy, string Current)[] Renames =
    [
        ("customSphereLeft", nameof(CreateTestRequest.SphereLeft)),
        ("customCylinderLeft", nameof(CreateTestRequest.CylinderLeft)),
        ("customAxisLeft", nameof(CreateTestRequest.AxisLeft)),
        ("customAddPowerLeft", nameof(CreateTestRequest.AddLeft)),
        ("customSphereRight", nameof(CreateTestRequest.SphereRight)),
        ("customCylinderRight", nameof(CreateTestRequest.CylinderRight)),
        ("customAxisRight", nameof(CreateTestRequest.AxisRight)),
        ("customAddPowerRight", nameof(CreateTestRequest.AddRight)),
    ];

    /// <summary>A <see cref="DefaultJsonTypeInfoResolver"/> modifier — pass it to
    /// <c>WithAddedModifier</c>.</summary>
    public static void Accept(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object
            || (typeInfo.Type != typeof(CreateTestRequest)
                && typeInfo.Type != typeof(CreateLeadRequest)
                && typeInfo.Type != typeof(CreateSaleRequest)))
        {
            return;
        }

        foreach (var (legacy, current) in Renames)
        {
            // The resolver's own accessors for the renamed property, rather than reflection of our
            // own — this also runs in the trimmed Field App.
            var property = typeInfo.Properties.Single(p => p.AttributeProvider is MemberInfo { Name: var name } && name == current);
            var get = property.Get!;
            var set = property.Set!;

            var alias = typeInfo.CreateJsonPropertyInfo(typeof(decimal?), legacy);
            alias.Set = (target, value) =>
            {
                if (value is decimal given && get(target) is null)
                {
                    set(target, given);
                }
            };

            typeInfo.Properties.Add(alias);
        }
    }
}
