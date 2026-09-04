using System.ComponentModel.DataAnnotations;

namespace DotGlasses.Web.Models;

public class ChangePasswordViewModel
{
    [Required]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The new password and confirmation don't match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? Error { get; set; }
}
