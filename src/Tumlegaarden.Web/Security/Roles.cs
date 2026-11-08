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

    /// <summary>Højere tal = flere rettigheder. Administrator 3, Diktator 2, Børler 1, ukendt 0.</summary>
    public static int Rank(string? role) => role is null ? 0 : Array.IndexOf(All, role) is var i and >= 0 ? All.Length - i : 0;

    /// <summary>Højeste rolle brugeren har.</summary>
    public static string? Of(System.Security.Claims.ClaimsPrincipal user) => All.FirstOrDefault(user.IsInRole);

    /// <summary>Diktator og administrator.</summary>
    public static bool IsModerator(System.Security.Claims.ClaimsPrincipal user) => Rank(Of(user)) >= Rank(Diktator);

    /// <summary>
    /// Må <paramref name="actor"/> redigere en bruger med rollen <paramref name="targetRole"/>?
    /// Administratorer må redigere alle (undtagen sig selv). Diktatorer kun brugere med lavere niveau.
    /// </summary>
    public static bool CanManage(System.Security.Claims.ClaimsPrincipal actor, string? targetRole)
    {
        var actorRank = Rank(Of(actor));
        if (actorRank == Rank(Administrator)) return true;
        return actorRank >= Rank(Diktator) && Rank(targetRole) < actorRank;
    }

    /// <summary>Roller <paramref name="actor"/> må tildele: administratorer alle, diktatorer op til diktator.</summary>
    public static IEnumerable<string> Assignable(System.Security.Claims.ClaimsPrincipal actor)
    {
        var actorRank = Rank(Of(actor));
        if (actorRank < Rank(Diktator)) return [];
        return All.Where(r => actorRank == Rank(Administrator) || Rank(r) <= actorRank);
    }
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
