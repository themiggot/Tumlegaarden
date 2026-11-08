using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Tumlegaarden.Web.Security;

namespace Tumlegaarden.Web.Pages.Account;

[AllowAnonymous]
public class LoginModel(
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    IOptions<SecurityOptions> options,
    ILogger<LoginModel> logger) : PageModel
{
    private const string GenericError = "Login mislykkedes. Tjek e-mail og adgangskode – og at din e-mail er bekræftet.";

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ReturnUrl { get; private set; }
    public string Domain => options.Value.AllowedEmailDomain;

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Angiv din e-mail.")]
        [EmailAddress(ErrorMessage = "Ugyldig e-mail.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Angiv din adgangskode.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl)
    {
        if (User.HasClaim("amr", "mfa")) return LocalRedirect(Url.SafeReturnUrl(returnUrl));
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        ReturnUrl = Url.SafeReturnUrl(returnUrl);
        if (!ModelState.IsValid) return Page();

        var email = Input.Email.Trim();
        if (!EmailDomain.IsAllowed(email, Domain))
        {
            ModelState.AddModelError(string.Empty, $"Kun e-mailadresser på @{Domain} kan logge ind.");
            return Page();
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Brug samme tid som et rigtigt login, så ukendte konti ikke kan spores via svartid.
            userManager.PasswordHasher.HashPassword(new IdentityUser(), Input.Password);
            ModelState.AddModelError(string.Empty, GenericError);
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(user, Input.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.RequiresTwoFactor)
            return RedirectToPage("./LoginWith2fa", new { returnUrl = ReturnUrl });

        if (result.Succeeded)
            // Korrekt adgangskode, men to-faktor er endnu ikke sat op – det er obligatorisk.
            return RedirectToPage("./SetupAuthenticator", new { returnUrl = ReturnUrl });

        if (result.IsLockedOut)
        {
            logger.LogWarning("Konto låst efter for mange forsøg.");
            ModelState.AddModelError(string.Empty, "For mange fejlede forsøg. Kontoen er låst i 15 minutter.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, GenericError);
        return Page();
    }
}
