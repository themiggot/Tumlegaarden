using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Tumlegaarden.Web.Security;

/// <summary>
/// Tjekker adgangskoden mod Have I Been Pwned via k-anonymity:
/// kun de første 5 tegn af SHA-1-hashen forlader serveren – aldrig adgangskoden.
/// Fejler åbent (logger en advarsel) hvis tjenesten ikke svarer.
/// </summary>
public sealed class PwnedPasswordValidator(
    IHttpClientFactory httpClientFactory,
    IOptions<SecurityOptions> options,
    ILogger<PwnedPasswordValidator> logger) : IPasswordValidator<IdentityUser>
{
    public const string HttpClientName = "pwned-passwords";

    public async Task<IdentityResult> ValidateAsync(UserManager<IdentityUser> manager, IdentityUser user, string? password)
    {
        if (!options.Value.CheckPwnedPasswords || string.IsNullOrEmpty(password)) return IdentityResult.Success;

        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
        var prefix = hash[..5];
        var suffix = hash[5..];

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"range/{prefix}");
            request.Headers.Add("Add-Padding", "true");
            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();

            foreach (var line in body.Split('\n'))
            {
                var parts = line.Trim().Split(':');
                if (parts.Length == 2 && parts[0].Equals(suffix, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(parts[1], out var count) && count > 0)
                {
                    return IdentityResult.Failed(new IdentityError
                    {
                        Code = "PasswordPwned",
                        Description = "Adgangskoden findes i kendte datalæk og kan ikke bruges. Vælg en anden."
                    });
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Kunne ikke kontakte Pwned Passwords – springer tjekket over.");
        }

        return IdentityResult.Success;
    }
}
