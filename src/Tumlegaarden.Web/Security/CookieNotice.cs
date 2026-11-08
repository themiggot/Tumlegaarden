namespace Tumlegaarden.Web.Security;

public static class CookieNotice
{
    /// <summary>Husker at brugeren har set cookie-meddelelsen. Selv en nødvendig cookie.</summary>
    public const string CookieName = "__Host-Tumle-Cookies";

    /// <summary>Husker at brugeren har bekræftet disclaimeren (privat side, kun for inviterede).</summary>
    public const string DisclaimerCookieName = "__Host-Tumle-Disclaimer";

    public static CookieOptions Options() => new()
    {
        Path = "/",
        Secure = true,
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        MaxAge = TimeSpan.FromDays(365)
    };
}
