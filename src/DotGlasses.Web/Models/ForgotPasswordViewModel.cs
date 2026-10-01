using System.ComponentModel.DataAnnotations;

namespace DotGlasses.Web.Models;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Enter your email address.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>True once a request has been made: the page then shows the one message it shows
    /// for every address, whether or not an email went out.</summary>
    public bool Sent { get; set; }
}
