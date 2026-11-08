using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Tumlegaarden.Web.Pages.Account;

internal static class AccountHelpers
{
    /// <summary>Kun lokale returadresser – forhindrer open redirect.</summary>
    public static string SafeReturnUrl(this IUrlHelper url, string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && url.IsLocalUrl(returnUrl) ? returnUrl : "/";

    public static string EncodeToken(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    public static string? DecodeToken(string? code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)); }
        catch (FormatException) { return null; }
    }

    public static string NormalizeCode(string code) => code.Replace(" ", "").Replace("-", "").Trim();
}
