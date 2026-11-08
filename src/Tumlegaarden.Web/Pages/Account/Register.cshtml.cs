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
public class RegisterModel(
    UserManager<IdentityUser> userManager,
    IAppEmailSender emailSender,
    IOptions<SecurityOptions> options) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string Domain => options.Value.AllowedEmailDomain;

    public sealed class InputModel
    {
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

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var email = Input.Email.Trim();
        if (!EmailDomain.IsAllowed(email, Domain))
        {
            ModelState.AddModelError(string.Empty, $"Kun e-mailadresser på @{Domain} kan oprettes.");
            return Page();
        }

        var candidate = new IdentityUser { UserName = email, Email = email };

        // Valider adgangskoden FØR vi ser om kontoen findes, så svaret ikke afslører eksisterende konti.
        foreach (var validator in userManager.PasswordValidators)
        {
            var check = await validator.ValidateAsync(userManager, candidate, Input.Password);
            if (!check.Succeeded)
            {
                foreach (var e in check.Errors.DistinctBy(e => e.Code)) ModelState.AddModelError(string.Empty, e.Description);
                return Page();
            }
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            var resetUrl = Url.Page("/Account/ForgotPassword", null, null, Request.Scheme);
            await emailSender.SendAsync(email, "Tumlegaarden: Du har allerede en konto",
                $"Nogen (forhåbentlig dig) forsøgte at oprette en konto med denne adresse, men den findes allerede.\n\n" +
                $"Har du glemt din adgangskode, kan du nulstille den her:\n{resetUrl}\n\n" +
                "Var det ikke dig, kan du se bort fra denne mail.");
            return RedirectToPage("./RegisterConfirmation");
        }

        var result = await userManager.CreateAsync(candidate, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(candidate);
        var link = Url.Page("/Account/ConfirmEmail", null,
            new { userId = candidate.Id, code = AccountHelpers.EncodeToken(token) }, Request.Scheme);

        await emailSender.SendAsync(email, "Tumlegaarden: Bekræft din e-mail",
            $"Velkommen til Tumlegaarden!\n\nBekræft din e-mailadresse ved at åbne dette link (gyldigt i 2 timer):\n{link}\n\n" +
            "Har du ikke oprettet en konto, kan du se bort fra denne mail.");

        return RedirectToPage("./RegisterConfirmation");
    }
}
