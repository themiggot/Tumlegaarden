using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Tumlegaarden.Web.Pages.Account;
using Tumlegaarden.Web.Security;

namespace Tumlegaarden.Web.Pages;

[AllowAnonymous]
public class CookiesModel : PageModel
{
    public void OnGet() { }

    public IActionResult OnPostAccept(string? returnUrl)
    {
        Response.Cookies.Append(CookieNotice.CookieName, "1", new CookieOptions
        {
            Path = "/",
            Secure = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            MaxAge = TimeSpan.FromDays(365)
        });
        return LocalRedirect(Url.SafeReturnUrl(returnUrl));
    }
}
