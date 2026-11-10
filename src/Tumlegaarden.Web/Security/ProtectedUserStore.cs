using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;

namespace Tumlegaarden.Web.Security;

/// <summary>
/// Identitys brugerlager, men to-faktor-hemmelighederne gemmes beskyttet i AspNetUserTokens:
/// <list type="bullet">
/// <item>Authenticator-nøglen krypteres med ASP.NET Data Protection (nøglerne ligger i datamappen,
///   ikke i databasen). Den skal kunne læses igen for at tjekke koder fra appen.</item>
/// <item>Gendannelseskoderne hashes med PBKDF2 og salt pr. kode. De skal kun kunne genkendes.</item>
/// </list>
/// Et læk af databasen alene afslører derfor hverken nøgler eller koder.
/// Ældre værdier i klartekst konverteres ved opstart (<see cref="TwoFactorSecretMigrator"/>).
/// </summary>
public sealed class ProtectedUserStore(AppDbContext context, IDataProtectionProvider dataProtection, IdentityErrorDescriber? describer = null)
    : UserStore<IdentityUser, IdentityRole, AppDbContext>(context, describer)
{
    private readonly IDataProtector _protector = TwoFactorSecrets.CreateProtector(dataProtection);

    public override Task SetAuthenticatorKeyAsync(IdentityUser user, string key, CancellationToken cancellationToken = default) =>
        base.SetAuthenticatorKeyAsync(user, TwoFactorSecrets.ProtectKey(_protector, key), cancellationToken);

    public override async Task<string?> GetAuthenticatorKeyAsync(IdentityUser user, CancellationToken cancellationToken = default) =>
        TwoFactorSecrets.UnprotectKey(_protector, await base.GetAuthenticatorKeyAsync(user, cancellationToken));

    public override Task ReplaceCodesAsync(IdentityUser user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken = default) =>
        SetTokenAsync(user, TwoFactorSecrets.LoginProvider, TwoFactorSecrets.RecoveryCodesName,
            TwoFactorSecrets.HashCodes(recoveryCodes), cancellationToken);

    public override async Task<bool> RedeemCodeAsync(IdentityUser user, string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(code);
        var stored = await GetTokenAsync(user, TwoFactorSecrets.LoginProvider, TwoFactorSecrets.RecoveryCodesName, cancellationToken) ?? "";
        var remaining = TwoFactorSecrets.Redeem(stored, code);
        if (remaining is null) return false;

        await SetTokenAsync(user, TwoFactorSecrets.LoginProvider, TwoFactorSecrets.RecoveryCodesName, remaining, cancellationToken);
        return true;
    }

    public override async Task<int> CountCodesAsync(IdentityUser user, CancellationToken cancellationToken = default) =>
        TwoFactorSecrets.Count(await GetTokenAsync(user, TwoFactorSecrets.LoginProvider, TwoFactorSecrets.RecoveryCodesName, cancellationToken) ?? "");
}

/// <summary>Formater og algoritmer for de beskyttede værdier. Fælles for lageret og konverteringen.</summary>
public static class TwoFactorSecrets
{
    public const string LoginProvider = "[AspNetUserStore]";
    public const string AuthenticatorKeyName = "AuthenticatorKey";
    public const string RecoveryCodesName = "RecoveryCodes";

    /// <summary>Præfiks for krypteret nøgle. Uden præfiks = gammel klartekst.</summary>
    private const string KeyPrefix = "dp1:";
    /// <summary>Præfiks for hashede koder. Uden præfiks = gammel klartekst.</summary>
    private const string CodesPrefix = "h1:";
    private const int Iterations = 20_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public static IDataProtector CreateProtector(IDataProtectionProvider provider) =>
        provider.CreateProtector("Tumlegaarden.TwoFactor.AuthenticatorKey.v1");

    public static bool IsProtectedKey(string? value) => value?.StartsWith(KeyPrefix, StringComparison.Ordinal) == true;
    public static bool IsHashedCodes(string? value) => value?.StartsWith(CodesPrefix, StringComparison.Ordinal) == true;

    public static string ProtectKey(IDataProtector protector, string key) => KeyPrefix + protector.Protect(key);

    /// <summary>Dekrypterer. Gammel klartekst (fra før kryptering) returneres som den er.</summary>
    public static string? UnprotectKey(IDataProtector protector, string? stored) =>
        IsProtectedKey(stored) ? protector.Unprotect(stored![KeyPrefix.Length..]) : stored;

    /// <summary>Koder sammenlignes uden mellemrum og bindestreg og uden forskel på store/små bogstaver.</summary>
    private static string Normalize(string code) =>
        new string(code.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();

    public static string HashCodes(IEnumerable<string> codes) =>
        CodesPrefix + string.Join(';', codes.Select(HashOne));

    private static string HashOne(string code)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(Normalize(code)), salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool Matches(string entry, string normalizedCode)
    {
        var dot = entry.IndexOf('.');
        if (dot <= 0) return false;
        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(entry[..dot]);
            expected = Convert.FromBase64String(entry[(dot + 1)..]);
        }
        catch (FormatException)
        {
            return false;
        }
        var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(normalizedCode), salt, Iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static string[] Entries(string stored, out bool hashed)
    {
        hashed = IsHashedCodes(stored);
        var body = hashed ? stored[CodesPrefix.Length..] : stored;
        return body.Split(';', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Indløser en kode. Returnerer de resterende koder (i hashet form), eller null hvis koden ikke findes.
    /// Hver kode kan kun bruges én gang.
    /// </summary>
    public static string? Redeem(string stored, string code)
    {
        var entries = Entries(stored, out var hashed).ToList();
        var normalized = Normalize(code);
        if (normalized.Length == 0) return null;

        // Tjek alle koder (ikke stop ved første træf), så svartiden ikke afslører hvilken kode der passede.
        var match = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            var ok = hashed ? Matches(entries[i], normalized) : Normalize(entries[i]) == normalized;
            if (ok && match < 0) match = i;
        }
        if (match < 0) return null;

        entries.RemoveAt(match);
        return hashed ? CodesPrefix + string.Join(';', entries) : HashCodes(entries);
    }

    public static int Count(string stored) => Entries(stored, out _).Length;
}

/// <summary>
/// Konverterer to-faktor-hemmeligheder gemt i klartekst (fra før kryptering) ved opstart.
/// Kører hver gang, men ændrer kun værdier uden præfiks, så den er ufarlig at gentage.
/// </summary>
public static class TwoFactorSecretMigrator
{
    public static async Task<int> MigrateAsync(AppDbContext db, IDataProtectionProvider dataProtection, ILogger log)
    {
        var protector = TwoFactorSecrets.CreateProtector(dataProtection);
        var tokens = await db.Set<IdentityUserToken<string>>()
            .Where(t => t.LoginProvider == TwoFactorSecrets.LoginProvider
                     && (t.Name == TwoFactorSecrets.AuthenticatorKeyName || t.Name == TwoFactorSecrets.RecoveryCodesName))
            .ToListAsync();

        var changed = 0;
        foreach (var token in tokens)
        {
            if (string.IsNullOrEmpty(token.Value)) continue;
            if (token.Name == TwoFactorSecrets.AuthenticatorKeyName && !TwoFactorSecrets.IsProtectedKey(token.Value))
            {
                token.Value = TwoFactorSecrets.ProtectKey(protector, token.Value);
                changed++;
            }
            else if (token.Name == TwoFactorSecrets.RecoveryCodesName && !TwoFactorSecrets.IsHashedCodes(token.Value))
            {
                token.Value = TwoFactorSecrets.HashCodes(token.Value.Split(';', StringSplitOptions.RemoveEmptyEntries));
                changed++;
            }
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync();
            log.LogInformation("Krypterede/hashede {Count} to-faktor-værdier, der var gemt i klartekst.", changed);
        }
        return changed;
    }
}
