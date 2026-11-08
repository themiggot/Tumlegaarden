using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Tumlegaarden.Web.Pages.Account;

[AllowAnonymous]
public class ResetPasswordModel(UserManager<IdentityUser> userManager) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        public string Code { get; set; } = "";

        [Required(ErrorMessage = "Angiv din e-mail.")]
        [EmailAddress(ErrorMessage = "Ugyldig e-mail.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Angiv en adgangskode.")]
        [StringLength(128, ErrorMessage = "Adgangskoden må højst være 128 tegn.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Gentag adgangskoden.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Adgangskoderne er ikke ens.")]
        public string ConfirmPassword { get; set; } = "";
    }

    public void OnGet(string? code) => Input.Code = code ?? "";

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var token = AccountHelpers.DecodeToken(Input.Code);
        var user = await userManager.FindByEmailAsync(Input.Email.Trim());
        if (user is null || token is null)
        {
            // Afslør ikke om kontoen findes.
            ModelState.AddModelError(string.Empty, "Linket er ugyldigt eller udløbet.");
            return Page();
        }

        var result = await userManager.ResetPasswordAsync(user, token, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }

        // Nulstilling ændrer security stamp → alle andre sessioner logges ud.
        await userManager.ResetAccessFailedCountAsync(user);
        TempData["Status"] = "Adgangskoden er ændret. Log ind igen.";
        return RedirectToPage("./Login");
    }
}
