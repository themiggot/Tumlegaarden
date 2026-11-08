using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Tumlegaarden.Web.Pages.Account;

[Authorize(Policy = "PasswordOnly")]
public class LogoutModel(SignInManager<IdentityUser> signInManager) : PageModel
{
    public IActionResult OnGet() => Redirect("/");

    public async Task<IActionResult> OnPostAsync()
    {
        await signInManager.SignOutAsync();
        TempData["Status"] = "Du er logget ud.";
        return RedirectToPage("./Login");
    }
}
