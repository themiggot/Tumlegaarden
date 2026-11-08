using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Security;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Web.Pages.Admin;

// Adgang styres af konventionen AuthorizeFolder("/Admin", Administrator) i Program.cs.
public class UsersModel(
    UserManager<IdentityUser> userManager,
    InvitationService invitations,
    IAppEmailSender emailSender,
    IOptions<SecurityOptions> options,
    ILogger<UsersModel> logger) : PageModel
{
    [BindProperty] public InviteInput Invite { get; set; } = new();
    public List<UserRow> Users { get; private set; } = [];
    public List<Invitation> Pending { get; private set; } = [];
    public string Domain => options.Value.AllowedEmailDomain;
    public IEnumerable<SelectListItem> RoleItems => Roles.All.Select(r => new SelectListItem(r, r, r == Roles.Borler));
    public string? NewInviteLink { get; private set; }
    public string? NewInviteEmail { get; private set; }

    public sealed record UserRow(string Id, string Email, string Role, bool TwoFactor, bool Locked, bool IsSelf);

    public sealed class InviteInput
    {
        [Required(ErrorMessage = "Angiv en e-mail.")]
        [EmailAddress(ErrorMessage = "Ugyldig e-mail.")]
        public string Email { get; set; } = "";

        [Required]
        public string Role { get; set; } = Roles.Borler;
    }

    public static string Css(string role) => role.ToLowerInvariant().Replace("ø", "o");

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostInviteAsync()
    {
        var email = Invite.Email.Trim();
        if (ModelState.IsValid && !EmailDomain.IsAllowed(email, Domain))
            ModelState.AddModelError(string.Empty, $"Kun adresser på @{Domain} kan inviteres.");
        if (ModelState.IsValid && !Roles.IsValid(Invite.Role))
            ModelState.AddModelError(string.Empty, "Ukendt niveau.");
        if (ModelState.IsValid && await userManager.FindByEmailAsync(email) is not null)
            ModelState.AddModelError(string.Empty, "Der findes allerede en bruger med denne adresse.");

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var token = await invitations.CreateAsync(email, Invite.Role, User.Identity?.Name);
        var link = Url.Page("/Account/AcceptInvitation", null, new { token }, Request.Scheme)!;
        logger.LogInformation("{Admin} inviterede {Email} som {Role}.", User.Identity?.Name, email, Invite.Role);

        try
        {
            await emailSender.SendAsync(email, "Tumlegaarden: Du er inviteret", InvitationMail.Body(Invite.Role, link));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kunne ikke sende invitation til {Email}.", email);
            ModelState.AddModelError(string.Empty, "Mailen kunne ikke sendes. Giv linket nedenfor videre på anden vis.");
        }

        NewInviteLink = link;
        NewInviteEmail = email;
        Invite = new InviteInput();
        ModelState.Remove("Invite.Email");
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRevokeAsync(int id)
    {
        await invitations.RevokeAsync(id);
        return Done("Invitationen er trukket tilbage.");
    }

    public async Task<IActionResult> OnPostRoleAsync(string id, string role)
    {
        if (await OtherUserAsync(id) is not { } user || !Roles.IsValid(role)) return NotFound();

        var current = await userManager.GetRolesAsync(user);
        await userManager.RemoveFromRolesAsync(user, current);
        await userManager.AddToRoleAsync(user, role);
        await userManager.UpdateSecurityStampAsync(user);
        logger.LogInformation("{Admin} satte {UserId} til {Role}.", User.Identity?.Name, user.Id, role);
        return Done($"{user.Email} er nu {role}.");
    }

    public async Task<IActionResult> OnPostLockAsync(string id)
    {
        if (await OtherUserAsync(id) is not { } user) return NotFound();
        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        await userManager.UpdateSecurityStampAsync(user);
        return Done($"{user.Email} er låst.");
    }

    public async Task<IActionResult> OnPostUnlockAsync(string id)
    {
        if (await OtherUserAsync(id) is not { } user) return NotFound();
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        return Done($"{user.Email} er låst op.");
    }

    public async Task<IActionResult> OnPostReset2faAsync(string id)
    {
        if (await OtherUserAsync(id) is not { } user) return NotFound();
        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        await userManager.UpdateSecurityStampAsync(user);
        return Done($"To-faktor er nulstillet for {user.Email}. Brugeren skal sætte den op igen ved næste login.");
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id, bool confirm)
    {
        if (await OtherUserAsync(id) is not { } user) return NotFound();
        if (!confirm) return Done("Sæt flueben i Bekræft for at slette.");
        await userManager.DeleteAsync(user);
        logger.LogInformation("{Admin} slettede {UserId}.", User.Identity?.Name, user.Id);
        return Done($"{user.Email} er slettet.");
    }

    /// <summary>Administratorer kan ikke ændre sig selv – så der altid er mindst én administrator.</summary>
    private async Task<IdentityUser?> OtherUserAsync(string id)
    {
        if (id == userManager.GetUserId(User)) return null;
        return await userManager.FindByIdAsync(id);
    }

    private RedirectToPageResult Done(string message)
    {
        TempData["Status"] = message;
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var selfId = userManager.GetUserId(User);
        var now = DateTimeOffset.UtcNow;
        var all = await userManager.Users.OrderBy(u => u.Email).ToListAsync();
        var rows = new List<UserRow>();
        foreach (var u in all)
        {
            var role = (await userManager.GetRolesAsync(u)).FirstOrDefault() ?? "–";
            rows.Add(new UserRow(u.Id, u.Email ?? "", role, u.TwoFactorEnabled, u.LockoutEnd > now, u.Id == selfId));
        }
        Users = rows.OrderBy(r => Array.IndexOf(Roles.All, r.Role) is var i && i < 0 ? 99 : i).ThenBy(r => r.Email).ToList();
        Pending = await invitations.PendingAsync();
    }
}
