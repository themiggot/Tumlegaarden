using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QRCoder;

namespace Tumlegaarden.Web.Pages.Account;

/// <summary>
/// Obligatorisk opsætning af TOTP-to-faktor. Kræver kun adgangskode-login,
/// men brugeren kan ikke komme videre på sitet før 2FA er aktiveret.
/// </summary>
[Authorize(Policy = "PasswordOnly")]
public class SetupAuthenticatorModel(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    ILogger<SetupAuthenticatorModel> logger) : PageModel
{
    private const string Issuer = "Tumlegaarden";

    [BindProperty] public InputModel Input { get; set; } = new();
    public string SharedKey { get; private set; } = "";
    public string QrDataUri { get; private set; } = "";
    public string[]? RecoveryCodes { get; private set; }
    public string ReturnUrl { get; private set; } = "/";

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Angiv koden fra appen.")]
        [StringLength(7, MinimumLength = 6, ErrorMessage = "Koden er 6 cifre.")]
        public string Code { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl)
    {
        ReturnUrl = Url.SafeReturnUrl(returnUrl);
        var (user, redirect) = await GetUserOrRedirectAsync();
        if (redirect is not null) return redirect;

        await LoadKeyAsync(user!);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        ReturnUrl = Url.SafeReturnUrl(returnUrl);
        var (user, redirect) = await GetUserOrRedirectAsync();
        if (redirect is not null) return redirect;

        if (!ModelState.IsValid)
        {
            await LoadKeyAsync(user!);
            return Page();
        }

        var valid = await userManager.VerifyTwoFactorTokenAsync(
            user!, userManager.Options.Tokens.AuthenticatorTokenProvider, AccountHelpers.NormalizeCode(Input.Code));
        if (!valid)
        {
            ModelState.AddModelError(string.Empty, "Koden passer ikke. Tjek at telefonens ur er korrekt, og prøv igen.");
            await LoadKeyAsync(user!);
            return Page();
        }

        await userManager.SetTwoFactorEnabledAsync(user!, true);
        RecoveryCodes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user!, 10))?.ToArray();
        logger.LogInformation("Bruger {UserId} har aktiveret to-faktor.", user!.Id);

        // Brugeren har netop bevist begge faktorer → fuld session.
        await signInManager.SignInWithClaimsAsync(user!, isPersistent: false, [new Claim("amr", "mfa")]);
        return Page();
    }

    private async Task<(IdentityUser? User, IActionResult? Redirect)> GetUserOrRedirectAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await signInManager.SignOutAsync();
            return (null, RedirectToPage("./Login"));
        }

        if (await userManager.GetTwoFactorEnabledAsync(user))
        {
            if (User.HasClaim("amr", "mfa")) return (null, LocalRedirect(ReturnUrl));
            // 2FA er slået til, men sessionen er ikke 2FA-verificeret: start forfra.
            await signInManager.SignOutAsync();
            return (null, RedirectToPage("./Login"));
        }

        return (user, null);
    }

    private async Task LoadKeyAsync(IdentityUser user)
    {
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user);
            // ResetAuthenticatorKey ændrer security stamp – forny cookien så sessionen ikke ugyldiggøres.
            await signInManager.RefreshSignInAsync(user);
        }

        SharedKey = FormatKey(key!);
        var email = await userManager.GetEmailAsync(user) ?? "";
        var uri = $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(email)}" +
                  $"?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6&period=30";

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(8, [24, 20, 37], [255, 255, 255], drawQuietZones: true);
        QrDataUri = "data:image/png;base64," + Convert.ToBase64String(png);
    }

    private static string FormatKey(string key)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(key.AsSpan(i, Math.Min(4, key.Length - i)));
        }
        return sb.ToString().ToLowerInvariant();
    }
}
