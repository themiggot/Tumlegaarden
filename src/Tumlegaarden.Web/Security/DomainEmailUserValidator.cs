using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Tumlegaarden.Web.Security;

/// <summary>Sikrer på datalaget at ingen bruger kan eksistere uden en @tumlegaarden.dk-adresse.</summary>
public sealed class DomainEmailUserValidator(IOptions<SecurityOptions> options) : IUserValidator<IdentityUser>
{
    public async Task<IdentityResult> ValidateAsync(UserManager<IdentityUser> manager, IdentityUser user)
    {
        var domain = options.Value.AllowedEmailDomain;
        var email = await manager.GetEmailAsync(user);
        var userName = await manager.GetUserNameAsync(user);

        if (!EmailDomain.IsAllowed(email, domain))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "DomainNotAllowed",
                Description = $"Kun e-mailadresser på @{domain} kan bruges."
            });
        }

        if (!string.Equals(userName, email, StringComparison.OrdinalIgnoreCase))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "UserNameMustBeEmail",
                Description = "Brugernavnet skal være e-mailadressen."
            });
        }

        return IdentityResult.Success;
    }
}
