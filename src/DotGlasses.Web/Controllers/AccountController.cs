using DotGlasses.Infrastructure.Identity;
using DotGlasses.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

/// <summary>
/// Basic login page for the Admin Portal's cookie-authenticated MVC session, plus the anonymous
/// SetPassword page a User Directory invite/reset link points at (see CLAUDE.md's Admin Portal
/// wiring (User Directory screen) section) — real Identity password-reset tokens, no email
/// sending yet (IEmailSender is stubbed).
/// </summary>
public class AccountController(SignInManager<ApplicationUser> signInManager) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(model.UserName, model.Password, isPersistent: true, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            model.Error = "Invalid username or password.";
            return View(model);
        }

        var user = await signInManager.UserManager.FindByNameAsync(model.UserName);
        if (user is not null)
        {
            user.LastLoginUtc = DateTimeOffset.UtcNow;
            await signInManager.UserManager.UpdateAsync(user);
        }

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    [HttpGet]
    [AllowAnonymous]
    public IActionResult SetPassword(string userId, string token) => View(new SetPasswordViewModel { UserId = userId, Token = token });

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPassword(SetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await signInManager.UserManager.FindByIdAsync(model.UserId);
        if (user is null)
        {
            model.Error = "Invalid or expired link.";
            return View(model);
        }

        var result = await signInManager.UserManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (!result.Succeeded)
        {
            model.Error = string.Join(" ", result.Errors.Select(e => e.Description));
            return View(model);
        }

        user.EmailConfirmed = true;
        await signInManager.UserManager.UpdateAsync(user);

        TempData["Info"] = "Password set — you can now log in.";
        return RedirectToAction(nameof(Login));
    }

    /// <summary>Self-service account settings — reachable by any authenticated user regardless of
    /// role, unlike User Directory's admin-only "manage other users" screens (see CLAUDE.md's RBAC
    /// table: this page carries no policy beyond plain [Authorize], same as Dashboard/Organisations/
    /// Event History).</summary>
    [HttpGet]
    [Authorize]
    public IActionResult Settings() => View(new ChangePasswordViewModel());

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(nameof(Settings), model);
        }

        var user = await signInManager.UserManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var result = await signInManager.UserManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            model.Error = string.Join(" ", result.Errors.Select(e => e.Description));
            return View(nameof(Settings), model);
        }

        // ChangePasswordAsync doesn't refresh the auth cookie's security stamp on its own —
        // without this, the technician stays signed in on this device (expected) but any *other*
        // active session for the same account keeps working on the old password until it expires,
        // rather than being invalidated immediately.
        await signInManager.RefreshSignInAsync(user);

        TempData["Info"] = "Password changed.";
        return RedirectToAction(nameof(Settings));
    }
}
