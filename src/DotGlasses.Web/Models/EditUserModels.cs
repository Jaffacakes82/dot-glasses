using DotGlasses.Application.Organisations;
using DotGlasses.Application.Users;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Web.Models;

/// <summary>The one wording for "this change removes access at once", shared by the Edit user
/// page and the Organisations screen so both confirmations read alike.</summary>
public static class AccessChangeCopy
{
    public static string RetailPointRemoval(string userName, string retailPointName) =>
        $"Remove {userName} from {retailPointName}? It applies at once, and any records they haven't sent from the Field App for {retailPointName} will be refused.";

    public const string AdminDemotion = "Change this person from Admin to User? It applies at once: they lose every Admin action in the Admin Portal.";
}

/// <summary>One row of the shared organisation picker (Invite and Edit user).</summary>
public record OrgPickerRow(Guid Id, string Name, OrganisationLevel Level, int Depth, bool IsDeactivated, bool IsTicked)
{
    public string LevelLabel => OrganisationLevelLabels.For(Level);
}

/// <summary>
/// The organisation picker Invite and Edit share: the caller's visible organisations as an
/// indented tree with each one's level, plus — on Edit — any deactivated organisation the user is
/// already assigned to, ticked and marked, so it can be unticked. A deactivated organisation is
/// never offered for a new assignment.
/// </summary>
public record OrgPickerViewModel(string FieldName, string ElementId, IReadOnlyList<OrgPickerRow> Rows)
{
    public static OrgPickerViewModel Build(
        string elementId,
        IReadOnlyList<OrganisationAdminNode> visibleOrgs,
        IReadOnlyCollection<Guid> tickedIds,
        IEnumerable<UserEditAssignment>? deactivatedAssignments = null)
    {
        var byId = visibleOrgs.ToDictionary(o => o.Id);
        var byParent = visibleOrgs.ToLookup(o => o.ParentId);
        var rows = new List<OrgPickerRow>();

        // Roots first by level then name, children by name — the Organisations screen's order.
        foreach (var root in visibleOrgs
            .Where(o => o.ParentId is null || !byId.ContainsKey(o.ParentId.Value))
            .OrderBy(o => o.Level).ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase))
        {
            Add(root, depth: 0);
        }

        foreach (var assignment in deactivatedAssignments ?? [])
        {
            rows.Add(new OrgPickerRow(assignment.OrgNodeId, assignment.Name, assignment.Level, Depth: 0, IsDeactivated: true, IsTicked: true));
        }

        return new OrgPickerViewModel("OrgNodeIds", elementId, rows);

        void Add(OrganisationAdminNode node, int depth)
        {
            rows.Add(new OrgPickerRow(node.Id, node.Name, node.Level, depth, IsDeactivated: false, tickedIds.Contains(node.Id)));
            foreach (var child in byParent[node.Id].OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            {
                Add(child, depth + 1);
            }
        }
    }
}

/// <summary>What the Edit user page posts. The Loaded* fields are what the page was rendered
/// with: the server applies only the differences (see <see cref="UserEditPlan"/>), so an
/// assignment someone else added since is left alone.</summary>
public class EditUserRequest
{
    public Guid Id { get; set; }
    public string? FullName { get; set; }
    public string? LoadedFullName { get; set; }
    public string? Role { get; set; }
    public string? LoadedRole { get; set; }
    public List<Guid> OrgNodeIds { get; set; } = [];
    public List<Guid> LoadedOrgNodeIds { get; set; } = [];
}

public record EditUserViewModel(
    UserEditDetail User,
    bool CanChangeRole,
    string? RoleLockedReason,
    bool CanChangeName,
    OrgPickerViewModel Picker);
