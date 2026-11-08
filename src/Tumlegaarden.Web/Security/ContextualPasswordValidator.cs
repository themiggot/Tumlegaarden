using Microsoft.AspNetCore.Identity;

namespace Tumlegaarden.Web.Security;

/// <summary>Afviser forudsigelige adgangskoder ud over længde- og kompleksitetskravene.</summary>
public sealed class ContextualPasswordValidator : IPasswordValidator<IdentityUser>
{
    private static readonly string[] Forbidden =
    [
        "tumlegaarden", "tumlegården", "password", "passw0rd", "kodeord", "adgangskode",
        "123456", "654321", "qwerty", "asdfgh", "letmein", "welcome", "velkommen", "sommer", "vinter"
    ];

    public Task<IdentityResult> ValidateAsync(UserManager<IdentityUser> manager, IdentityUser user, string? password)
    {
        if (string.IsNullOrEmpty(password)) return Task.FromResult(IdentityResult.Success);

        var errors = new List<IdentityError>();
        var lower = password.ToLowerInvariant();

        var localPart = user.Email?.Split('@')[0].ToLowerInvariant();
        if (localPart is { Length: >= 3 } && lower.Contains(localPart))
        {
            errors.Add(new IdentityError { Code = "PasswordContainsEmail", Description = "Adgangskoden må ikke indeholde din e-mailadresse." });
        }

        if (Forbidden.Any(lower.Contains))
        {
            errors.Add(new IdentityError { Code = "PasswordTooCommon", Description = "Adgangskoden indeholder et alt for almindeligt ord eller mønster." });
        }

        for (var i = 3; i < password.Length; i++)
        {
            if (password[i] == password[i - 1] && password[i] == password[i - 2] && password[i] == password[i - 3])
            {
                errors.Add(new IdentityError { Code = "PasswordRepeats", Description = "Adgangskoden må ikke have samme tegn 4 gange i træk." });
                break;
            }
        }

        return Task.FromResult(errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed([.. errors]));
    }
}
