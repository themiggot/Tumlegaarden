using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Account;

/// <summary>Den eneste vej til en ny konto: et engangslink fra en administrator.</summary>
[AllowAnonymous]
public class AcceptInvitationModel(
    InvitationService invitations,
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    ILogger<AcceptInvitationModel> logger) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public Invitation? Invitation { get; private set; }
    public string? Token { get; private set; }
    public string RoleCss => Invitation?.Role.ToLowerInvariant().Replace("ø", "o") ?? "";

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Angiv en adgangskode.")]
        [StringLength(128, ErrorMessage = "Adgangskoden må højst være 128 tegn.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Gentag adgangskoden.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Adgangskoderne er ikke ens.")]
        public string ConfirmPassword { get; set; } = "";
    }

    public async Task OnGetAsync(string? token)
    {
        Token = token;
        Invitation = await invitations.FindValidAsync(token);
    }

    public async Task<IActionResult> OnPostAsync(string? token)
    {
        Token = token;
        Invitation = await invitations.FindValidAsync(token);
        if (Invitation is null || !ModelState.IsValid) return Page();

        if (await userManager.FindByEmailAsync(Invitation.Email) is not null)
        {
            ModelState.AddModelError(string.Empty, "Der findes allerede en konto med denne adresse.");
            return Page();
        }

        // E-mailen regnes som bekræftet: linket blev sendt til adressen af en administrator.
        var user = new IdentityUser { UserName = Invitation.Email, Email = Invitation.Email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }

        await userManager.AddToRoleAsync(user, Invitation.Role);
        await invitations.MarkUsedAsync(Invitation);
        logger.LogInformation("Bruger {UserId} oprettet via invitation {InvitationId} som {Role}.", user.Id, Invitation.Id, Invitation.Role);

        // Logget ind med adgangskode – næste skridt er obligatorisk to-faktor.
        await signInManager.SignInWithClaimsAsync(user, isPersistent: false, [new Claim("amr", "pwd")]);
        return RedirectToPage("./SetupAuthenticator");
    }
}
