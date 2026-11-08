using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Tumlegaarden.Web.Pages.Account;

[AllowAnonymous]
public class LoginWith2faModel(SignInManager<IdentityUser> signInManager) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ReturnUrl { get; private set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Angiv koden.")]
        [StringLength(7, MinimumLength = 6, ErrorMessage = "Koden er 6 cifre.")]
        public string Code { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl)
    {
        if (await signInManager.GetTwoFactorAuthenticationUserAsync() is null) return RedirectToPage("./Login");
        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        ReturnUrl = Url.SafeReturnUrl(returnUrl);
        if (!ModelState.IsValid) return Page();
        if (await signInManager.GetTwoFactorAuthenticationUserAsync() is null) return RedirectToPage("./Login");

        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(
            AccountHelpers.NormalizeCode(Input.Code), isPersistent: false, rememberClient: false);

        if (result.Succeeded) return LocalRedirect(ReturnUrl);
        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "For mange fejlede forsøg. Kontoen er låst i 15 minutter.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "Forkert kode.");
        return Page();
    }
}
