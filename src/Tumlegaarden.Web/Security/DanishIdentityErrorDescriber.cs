using Microsoft.AspNetCore.Identity;

namespace Tumlegaarden.Web.Security;

public sealed class DanishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => new() { Code = nameof(DefaultError), Description = "Der opstod en ukendt fejl." };
    public override IdentityError ConcurrencyFailure() => new() { Code = nameof(ConcurrencyFailure), Description = "Data blev ændret samtidigt. Prøv igen." };
    public override IdentityError PasswordMismatch() => new() { Code = nameof(PasswordMismatch), Description = "Forkert adgangskode." };
    public override IdentityError InvalidToken() => new() { Code = nameof(InvalidToken), Description = "Linket er ugyldigt eller udløbet." };
    public override IdentityError RecoveryCodeRedemptionFailed() => new() { Code = nameof(RecoveryCodeRedemptionFailed), Description = "Gendannelseskoden er ugyldig." };
    public override IdentityError LoginAlreadyAssociated() => new() { Code = nameof(LoginAlreadyAssociated), Description = "Login er allerede tilknyttet en anden bruger." };
    public override IdentityError InvalidUserName(string? userName) => new() { Code = nameof(InvalidUserName), Description = "Ugyldigt brugernavn." };
    public override IdentityError InvalidEmail(string? email) => new() { Code = nameof(InvalidEmail), Description = "Ugyldig e-mailadresse." };
    // Bevidst neutral tekst for at undgå at afsløre hvilke konti der findes.
    public override IdentityError DuplicateUserName(string userName) => new() { Code = nameof(DuplicateUserName), Description = "Kontoen kan ikke oprettes." };
    public override IdentityError DuplicateEmail(string email) => new() { Code = nameof(DuplicateEmail), Description = "Kontoen kan ikke oprettes." };
    public override IdentityError UserAlreadyHasPassword() => new() { Code = nameof(UserAlreadyHasPassword), Description = "Brugeren har allerede en adgangskode." };
    public override IdentityError UserLockoutNotEnabled() => new() { Code = nameof(UserLockoutNotEnabled), Description = "Kontolåsning er ikke aktiveret." };
    public override IdentityError PasswordTooShort(int length) => new() { Code = nameof(PasswordTooShort), Description = $"Adgangskoden skal være mindst {length} tegn." };
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"Adgangskoden skal indeholde mindst {uniqueChars} forskellige tegn." };
    public override IdentityError PasswordRequiresNonAlphanumeric() => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "Adgangskoden skal indeholde mindst ét specialtegn (fx ! # % &)." };
    public override IdentityError PasswordRequiresDigit() => new() { Code = nameof(PasswordRequiresDigit), Description = "Adgangskoden skal indeholde mindst ét tal." };
    public override IdentityError PasswordRequiresLower() => new() { Code = nameof(PasswordRequiresLower), Description = "Adgangskoden skal indeholde mindst ét lille bogstav." };
    public override IdentityError PasswordRequiresUpper() => new() { Code = nameof(PasswordRequiresUpper), Description = "Adgangskoden skal indeholde mindst ét stort bogstav." };
}
