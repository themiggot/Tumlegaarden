using Microsoft.AspNetCore.Identity;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Security;

namespace Tumlegaarden.Web.Services;

public static class StartupTasks
{
    /// <summary>
    /// Opretter databasen og rollerne. Findes der ingen brugere endnu, sendes en
    /// administrator-invitation til Bootstrap:AdminEmail – det er den eneste måde
    /// den første bruger kan oprettes på.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        await sp.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var userManager = sp.GetRequiredService<UserManager<IdentityUser>>();
        if (userManager.Users.Any()) return;

        var config = sp.GetRequiredService<IConfiguration>();
        var email = config["Bootstrap:AdminEmail"];
        var baseUrl = config["App:PublicBaseUrl"]?.TrimEnd('/');
        var domain = config["Security:AllowedEmailDomain"] ?? "tumlegaarden.dk";

        if (!EmailDomain.IsAllowed(email, domain) || string.IsNullOrEmpty(baseUrl))
        {
            log.LogWarning("Ingen brugere og ingen gyldig Bootstrap:AdminEmail/App:PublicBaseUrl – kan ikke invitere første administrator.");
            return;
        }

        var invitations = sp.GetRequiredService<InvitationService>();
        if (await invitations.AnyPendingAsync()) return;

        var token = await invitations.CreateAsync(email!, Roles.Administrator, "system");
        try
        {
            await sp.GetRequiredService<IAppEmailSender>().SendAsync(email!, "Tumlegaarden: Du er inviteret som administrator",
                InvitationMail.Body(Roles.Administrator, $"{baseUrl}/Account/AcceptInvitation?token={token}"));
            log.LogInformation("Administrator-invitation sendt til {Email}.", email);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Kunne ikke sende administrator-invitation.");
        }
    }
}

public static class InvitationMail
{
    public static string Body(string role, string link) =>
        $"Du er inviteret til Tumlegaarden som {role}.\n\n" +
        $"Åbn linket for at vælge din adgangskode og sætte to-faktor op. Linket virker én gang og udløber om {InvitationService.Lifetime.TotalHours:0} timer:\n{link}\n\n" +
        "Forventede du ikke denne mail, kan du se bort fra den.";
}
