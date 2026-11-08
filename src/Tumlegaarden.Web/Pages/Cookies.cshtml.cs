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
        Response.Cookies.Append(CookieNotice.CookieName, "1", CookieNotice.Options());
        return LocalRedirect(Url.SafeReturnUrl(returnUrl));
    }

    /// <summary>Disclaimeren indeholder også cookie-informationen, så begge huskes.</summary>
    public IActionResult OnPostDisclaimer(string? returnUrl)
    {
        Response.Cookies.Append(CookieNotice.DisclaimerCookieName, "1", CookieNotice.Options());
        Response.Cookies.Append(CookieNotice.CookieName, "1", CookieNotice.Options());
        return LocalRedirect(Url.SafeReturnUrl(returnUrl));
    }
}
