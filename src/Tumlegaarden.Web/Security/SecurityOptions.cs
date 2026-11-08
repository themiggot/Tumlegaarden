namespace Tumlegaarden.Web.Security;

public sealed class SecurityOptions
{
    /// <summary>Det eneste e-maildomæne der må oprette konto og logge ind.</summary>
    public string AllowedEmailDomain { get; set; } = "tumlegaarden.dk";

    /// <summary>Afvis adgangskoder der findes i kendte datalæk (Have I Been Pwned, k-anonymity).</summary>
    public bool CheckPwnedPasswords { get; set; } = true;

    /// <summary>Maks. antal POST-forsøg pr. minut pr. IP på konto-siderne.</summary>
    public int AuthPostsPerMinute { get; set; } = 30;

    /// <summary>Kun adgang fra danske IP-adresser.</summary>
    public bool DenmarkOnly { get; set; } = true;

    /// <summary>Ekstra tilladte adresser eller net (fx "203.0.113.7" eller "198.51.100.0/24") uanset land.</summary>
    public string[] AllowedIpRanges { get; set; } = [];
}
