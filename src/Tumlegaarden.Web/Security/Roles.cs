using Microsoft.AspNetCore.Authorization;

namespace Tumlegaarden.Web.Security;

/// <summary>Brugerniveauer i faldende rækkefølge af rettigheder.</summary>
public static class Roles
{
    public const string Administrator = "Administrator";
    public const string Diktator = "Diktator";
    public const string Borler = "Børler";

    /// <summary>Højeste rettighed først.</summary>
    public static readonly string[] All = [Administrator, Diktator, Borler];

    public static bool IsValid(string? role) => role is not null && All.Contains(role);

    /// <summary>Alle roller med mindst samme rettigheder som <paramref name="minimum"/>.</summary>
    public static string[] AtLeast(string minimum) => All[..(Array.IndexOf(All, minimum) + 1)];
}

public static class Policies
{
    /// <summary>Kun adgangskode-login (bruges til opsætning af 2FA og log ud).</summary>
    public const string PasswordOnly = "PasswordOnly";
    public const string Administrator = "Administrator";
    public const string DiktatorOrHigher = "DiktatorOrHigher";
    public const string BorlerOrHigher = "BorlerOrHigher";

    public static AuthorizationPolicy RoleAtLeast(string role) => new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim("amr", "mfa")
        .RequireRole(Roles.AtLeast(role))
        .Build();
}
