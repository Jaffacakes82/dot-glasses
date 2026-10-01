using System.ComponentModel.DataAnnotations;

namespace DotGlasses.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your email address.")]
    [Display(Name = "Email")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }

    public string? Error { get; set; }
}
