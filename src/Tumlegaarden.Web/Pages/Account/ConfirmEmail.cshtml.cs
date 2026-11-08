using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Tumlegaarden.Web.Pages.Account;

// Bekræftelse sker via POST, så mailscannere der "klikker" på links ikke aktiverer kontoen.
[AllowAnonymous]
public class ConfirmEmailModel(UserManager<IdentityUser> userManager) : PageModel
{
    public string? UserId { get; private set; }
    public string? Code { get; private set; }
    public bool Invalid { get; private set; }

    public void OnGet(string? userId, string? code)
    {
        UserId = userId;
        Code = code;
        Invalid = string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code);
    }

    public async Task<IActionResult> OnPostAsync(string? userId, string? code)
    {
        var token = AccountHelpers.DecodeToken(code);
        var user = string.IsNullOrEmpty(userId) ? null : await userManager.FindByIdAsync(userId);
        if (user is null || token is null)
        {
            Invalid = true;
            return Page();
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            Invalid = true;
            return Page();
        }

        TempData["Status"] = "E-mail bekræftet. Log ind for at sætte to-faktor op.";
        return RedirectToPage("./Login");
    }
}
