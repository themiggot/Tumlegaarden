using System.Net.Mail;

namespace Tumlegaarden.Web.Security;

public static class EmailDomain
{
    /// <summary>
    /// Streng kontrol: præcis én '@', ingen display-navne/whitespace,
    /// og domænet skal matche eksakt (ingen subdomæner, ingen "tumlegaarden.dk.evil.com").
    /// </summary>
    public static bool IsAllowed(string? email, string allowedDomain)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254) return false;
        if (email.Trim() != email || email.Any(char.IsWhiteSpace)) return false;
        if (email.Count(c => c == '@') != 1) return false;
        if (!MailAddress.TryCreate(email, out var address)) return false;
        if (!string.Equals(address.Address, email, StringComparison.Ordinal)) return false;
        if (address.User.Length is 0 or > 64) return false;
        return string.Equals(address.Host, allowedDomain, StringComparison.OrdinalIgnoreCase);
    }
}
