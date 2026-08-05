using DotGlasses.Application.Notifications;
using DotGlasses.Application.Organisations;
using DotGlasses.Application.Users;
using DotGlasses.Web.Authorization;
using DotGlasses.Web.Models;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

[Authorize]
public class UserDirectoryController(
    IUserAdminService userAdminService,
    IOrganisationAdminService organisationAdminService,
    IAuthorizationService authorizationService,
    IValidator<InviteUserRequest> inviteValidator,
    IEmailSender emailSender) : Controller
{
    private static readonly Dictionary<string, string> StatusColor = new()
    {
        ["Active"] = "var(--dot-green)",
        ["Invited"] = "var(--dot-yellow)",
        ["Suspended"] = "#cccccc",
    };

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["StatusColor"] = StatusColor;
        ViewData["AvailableOrgs"] = await organisationAdminService.ListAsync(cancellationToken);
        return View(await BuildUserListAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(InviteUserRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await inviteValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            ViewData["StatusColor"] = StatusColor;
            ViewData["AvailableOrgs"] = await organisationAdminService.ListAsync(cancellationToken);
            return View(nameof(Index), await BuildUserListAsync(cancellationToken));
        }

        if (!await CanManageOrgAsync(request.OrgNodeIds[0], cancellationToken))
        {
            return Forbid();
        }

        var result = await userAdminService.InviteAsync(request.Email, request.FullName, request.Role, request.OrgNodeIds, cancellationToken);
        var setPasswordUrl = Url.Action(nameof(AccountController.SetPassword), "Account", new { userId = result.UserId, token = result.PasswordResetToken }, Request.Scheme)!;

        await emailSender.SendPasswordSetupInviteAsync(result.Email, request.FullName, setPasswordUrl, cancellationToken);
        TempData["SetPasswordLink"] = setPasswordUrl;
        TempData["SetPasswordLinkFor"] = result.Email;

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken cancellationToken)
    {
        var target = await FindManageableUserAsync(id, cancellationToken);
        if (target is null)
        {
            return Forbid();
        }

        var token = await userAdminService.RegeneratePasswordResetTokenAsync(id, cancellationToken);
        var setPasswordUrl = Url.Action(nameof(AccountController.SetPassword), "Account", new { userId = id, token }, Request.Scheme)!;

        await emailSender.SendPasswordSetupInviteAsync(target.Email, target.DisplayName, setPasswordUrl, cancellationToken);
        TempData["SetPasswordLink"] = setPasswordUrl;
        TempData["SetPasswordLinkFor"] = target.Email;

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken cancellationToken)
    {
        if (await FindManageableUserAsync(id, cancellationToken) is null)
        {
            return Forbid();
        }

        await userAdminService.SuspendAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unsuspend(Guid id, CancellationToken cancellationToken)
    {
        if (await FindManageableUserAsync(id, cancellationToken) is null)
        {
            return Forbid();
        }

        await userAdminService.UnsuspendAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> CanManageOrgAsync(Guid orgNodeId, CancellationToken cancellationToken)
    {
        var org = (await organisationAdminService.ListAsync(cancellationToken)).FirstOrDefault(o => o.Id == orgNodeId);
        if (org is null)
        {
            return false;
        }

        var result = await authorizationService.AuthorizeAsync(User, org.HierarchyPath, AuthorizationPolicies.ManageUsersInScope);
        return result.Succeeded;
    }

    /// <summary>Resource-based check against the target user's own HierarchyPath — re-checked
    /// here even though the view already hides the triggering form for a user who'd fail it;
    /// never trust the hidden-button UX alone.</summary>
    private async Task<UserAdminRow?> FindManageableUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var target = (await userAdminService.ListAsync(cancellationToken)).FirstOrDefault(u => u.Id == userId);
        if (target is null)
        {
            return null;
        }

        var result = await authorizationService.AuthorizeAsync(User, target.HierarchyPath, AuthorizationPolicies.ManageUsersInScope);
        return result.Succeeded ? target : null;
    }

    private async Task<IReadOnlyList<DirectoryUser>> BuildUserListAsync(CancellationToken cancellationToken)
    {
        var rows = await userAdminService.ListAsync(cancellationToken);
        var result = new List<DirectoryUser>();
        foreach (var row in rows)
        {
            var canManage = (await authorizationService.AuthorizeAsync(User, row.HierarchyPath, AuthorizationPolicies.ManageUsersInScope)).Succeeded;
            result.Add(new DirectoryUser(
                row.Id,
                row.DisplayName,
                row.Role,
                row.OrgNames,
                row.Status,
                row.LastLoginUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—",
                row.SalesCount.ToString(),
                canManage));
        }

        return result;
    }
}
