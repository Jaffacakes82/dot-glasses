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

    public string? Error { get; set; }
}
