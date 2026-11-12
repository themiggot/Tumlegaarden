using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Security;

namespace Tumlegaarden.Tests.Integration;

/// <summary>
/// Hele sitet i hukommelsen med en SQLite-database i en midlertidig mappe. Login sker med headeren
/// <c>X-Test-User</c> (brugerens e-mail) i stedet for adgangskode og to-faktor; alt andet – roller,
/// rettigheder, antiforgery, database – kører som i produktion.
/// </summary>
public sealed class TestApp : WebApplicationFactory<Program>
{
    public const string UserHeader = "X-Test-User";
    private readonly string _dataDir = Directory.CreateTempSubdirectory("tumle-test-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("App:DataPath", _dataDir);
        builder.UseSetting("ConnectionStrings:MySql", "");
        builder.UseSetting("Logs:Directory", Path.Combine(_dataDir, "logs"));
        builder.UseSetting("Security:DenmarkOnly", "false");
        builder.UseSetting("Security:CheckPwnedPasswords", "false");
        builder.UseSetting("Security:AuthPostsPerMinute", "1000");
        builder.UseSetting("AllowedHosts", "*");
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(o => o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(_dataDir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Opretter en bruger med en rolle (ingen adgangskode – login sker via testheaderen).</summary>
    public async Task<string> CreateUserAsync(string name, string role = Roles.Borler)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var email = $"{name}@tumlegaarden.dk";
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(user);
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, role);
        return user.Id;
    }

    /// <summary>En browser, der er logget ind som brugeren (eller anonym ved null). Følger ikke redirects.</summary>
    public HttpClient ClientFor(string? name)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (name is not null) client.DefaultRequestHeaders.Add(UserHeader, $"{name}@tumlegaarden.dk");
        return client;
    }

    public T Db<T>(Func<AppDbContext, T> query)
    {
        using var scope = Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<T> WithServicesAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider);
    }
}

/// <summary>Logger brugeren fra <see cref="TestApp.UserHeader"/> ind med roller og to-faktor (amr=mfa).</summary>
public sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, Microsoft.Extensions.Logging.ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(TestApp.UserHeader, out var email) || string.IsNullOrEmpty(email)) return AuthenticateResult.NoResult();
        var users = Context.RequestServices.GetRequiredService<UserManager<IdentityUser>>();
        var user = await users.FindByEmailAsync(email!);
        if (user is null) return AuthenticateResult.Fail("Ukendt testbruger");
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName!),
            new(ClaimTypes.Email, user.Email!),
            new("amr", "mfa")
        };
        claims.AddRange((await users.GetRolesAsync(user)).Select(r => new Claim(ClaimTypes.Role, r)));
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName));
    }
}

/// <summary>Hjælpere til at hente sider og sende formularer med antiforgery-token.</summary>
public static partial class Http
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();

    public static async Task<string> PageAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {url}: {(int)response.StatusCode}");
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Sender en formular. Token hentes fra <paramref name="tokenFrom"/> (standard: samme side uden query).</summary>
    public static async Task<HttpResponseMessage> PostFormAsync(this HttpClient client, string url, IEnumerable<KeyValuePair<string, string>> fields, string? tokenFrom = null)
    {
        var html = await client.PageAsync(tokenFrom ?? url.Split('?')[0]);
        var token = TokenPattern().Match(html);
        Assert.True(token.Success, $"Intet antiforgery-token på {tokenFrom ?? url}");
        var form = fields.Append(new("__RequestVerificationToken", WebUtility.HtmlDecode(token.Groups[1].Value)));
        return await client.PostAsync(url, new FormUrlEncodedContent(form));
    }

    public static Task<HttpResponseMessage> PostFormAsync(this HttpClient client, string url, params (string Key, string Value)[] fields) =>
        client.PostFormAsync(url, fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));
}
