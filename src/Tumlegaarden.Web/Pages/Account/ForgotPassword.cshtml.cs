using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Tumlegaarden.Web.Security;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Account;

[AllowAnonymous]
public class ForgotPasswordModel(
    UserManager<IdentityUser> userManager,
    IAppEmailSender emailSender,
    IOptions<SecurityOptions> options) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public bool Sent { get; private set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Angiv din e-mail.")]
        [EmailAddress(ErrorMessage = "Ugyldig e-mail.")]
        public string Email { get; set; } = "";
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var email = Input.Email.Trim();
        if (EmailDomain.IsAllowed(email, options.Value.AllowedEmailDomain))
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is not null && await userManager.IsEmailConfirmedAsync(user))
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var link = Url.Page("/Account/ResetPassword", null,
                    new { code = AccountHelpers.EncodeToken(token) }, Request.Scheme);
                await emailSender.SendAsync(email, "Tumlegaarden: Nulstil adgangskode",
                    $"Åbn dette link for at vælge en ny adgangskode (gyldigt i 2 timer):\n{link}\n\n" +
                    "Har du ikke bedt om det, kan du se bort fra denne mail – din adgangskode er uændret.");
            }
        }

        // Samme svar uanset om kontoen findes.
        Sent = true;
        return Page();
    }
}
