using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Tumlegaarden.Web.Pages.Account;

[AllowAnonymous]
public class LoginWithRecoveryCodeModel(SignInManager<IdentityUser> signInManager) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ReturnUrl { get; private set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Angiv en gendannelseskode.")]
        [StringLength(32)]
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

        var result = await signInManager.TwoFactorRecoveryCodeSignInAsync(Input.Code.Replace(" ", "").Trim());
        if (result.Succeeded) return LocalRedirect(ReturnUrl);
        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "For mange fejlede forsøg. Kontoen er låst i 15 minutter.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "Ugyldig gendannelseskode.");
        return Page();
    }
}
