using System.ComponentModel.DataAnnotations;

namespace DotGlasses.Web.Models;

public class LoginViewModel
{
    [Required]
    [Display(Name = "Email")]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }

    public string? Error { get; set; }
}
