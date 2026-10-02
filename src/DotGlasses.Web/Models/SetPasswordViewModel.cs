using System.ComponentModel.DataAnnotations;

namespace DotGlasses.Web.Models;

public class SetPasswordViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Which sign-in page asked for the link (RequestingApp), so the person is returned
    /// to it. Absent on an invite or an admin's reset, which return to the Admin Portal.</summary>
    public string? App { get; set; }

    public string? Error { get; set; }
}
