using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Security;
using Tumlegaarden.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

// ---------- Lagring ----------
// Data og hemmeligheder ligger UDEN FOR web-mappen (fx /tumlegaarden-data ved siden af /public_html),
// så "Importer fra Git" hos Simply.com kan erstatte public_html uden at slette dem.
// Findes mappen ikke, bruges App_Data inde i appen (lokal udvikling).
var appData = DataDirectory.Resolve(builder.Configuration, builder.Environment);
Directory.CreateDirectory(appData);
builder.Configuration.AddJsonFile(Path.Combine(appData, "appsettings.Production.json"), optional: true, reloadOnChange: false);

// Uploads bufres i datamappen frem for i serverens fælles temp-mappe.
var paths = new AppPaths(appData);
Directory.CreateDirectory(paths.Temp);
Environment.SetEnvironmentVariable("ASPNETCORE_TEMP", paths.Temp);
builder.Services.AddSingleton(paths);
builder.Services.AddSingleton<PhotoStore>();
builder.Services.AddScoped<SiteSettings>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddHostedService<ChatCleanupService>();
builder.Services.AddSingleton(LogRetention.FromConfiguration(builder.Configuration, builder.Environment));
builder.Services.AddHostedService<LogCleanupService>();
builder.Services.AddSingleton<PresenceTracker>();
builder.Services.AddSingleton(GameModes.Load(Path.Combine(builder.Environment.ContentRootPath, "GameData")));
builder.Services.AddSingleton(MiniGames.Load(Path.Combine(builder.Environment.ContentRootPath, "GameData")));

// Database: MySQL (Simply.com) eller – hvis den ikke er sat op – en SQLite-fil i datamappen.
var mySql = builder.Configuration.GetConnectionString("MySql");
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (!string.IsNullOrWhiteSpace(mySql)) o.UseMySQL(mySql);
    else o.UseSqlite($"Data Source={Path.Combine(appData, "tumlegaarden.db")}");
});

// Nøgler til cookies/tokens overlever genstart af app-poolen på webhotellet.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(appData, "keys")))
    .SetApplicationName("Tumlegaarden");

builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddTransient<IAppEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<InvitationService>();
var dkRangesPath = Path.Combine(builder.Environment.ContentRootPath, "GeoData", "dk-ip-ranges.csv");
builder.Services.AddSingleton(File.Exists(dkRangesPath) ? CountryIpFilter.Load(dkRangesPath) : CountryIpFilter.Empty);
builder.Services.AddHttpClient(PwnedPasswordValidator.HttpClientName, c =>
{
    c.BaseAddress = new Uri("https://api.pwnedpasswords.com/");
    c.Timeout = TimeSpan.FromSeconds(5);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Tumlegaarden-Web");
});

// ---------- Identity ----------
builder.Services.AddIdentity<IdentityUser, IdentityRole>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.SignIn.RequireConfirmedEmail = true;
        o.SignIn.RequireConfirmedAccount = true;

        o.Password.RequiredLength = 14;
        o.Password.RequiredUniqueChars = 6;
        o.Password.RequireDigit = true;
        o.Password.RequireLowercase = true;
        o.Password.RequireUppercase = true;
        o.Password.RequireNonAlphanumeric = true;

        o.Lockout.AllowedForNewUsers = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddUserStore<ProtectedUserStore>()   // krypterer authenticator-nøgler og hasher gendannelseskoder
    .AddDefaultTokenProviders()
    .AddErrorDescriber<DanishIdentityErrorDescriber>()
    .AddUserValidator<DomainEmailUserValidator>()
    .AddPasswordValidator<ContextualPasswordValidator>()
    .AddPasswordValidator<PwnedPasswordValidator>();

// PBKDF2-HMAC-SHA512 med OWASP's anbefalede antal iterationer.
builder.Services.Configure<PasswordHasherOptions>(o => o.IterationCount = 210_000);
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(2));
builder.Services.Configure<SecurityStampValidatorOptions>(o =>
{
    // Rolleskift, låsning og sletning slår igennem på aktive sessioner inden for et minut.
    o.ValidationInterval = TimeSpan.FromMinutes(1);
    // Bevar "amr=mfa" når cookien fornyes, ellers ville brugeren miste 2FA-status.
    o.OnRefreshingPrincipal = ctx =>
    {
        if (ctx.CurrentPrincipal?.HasClaim("amr", "mfa") == true && ctx.NewPrincipal?.Identity is ClaimsIdentity id
            && !id.HasClaim("amr", "mfa"))
        {
            id.AddClaim(new Claim("amr", "mfa"));
        }
        return Task.CompletedTask;
    };
});

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "__Host-Tumle";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    o.SlidingExpiration = true;
    o.LoginPath = "/Account/Login";
    o.LogoutPath = "/Account/Logout";
    o.AccessDeniedPath = "/Account/SetupAuthenticator";
    o.Events.OnRedirectToAccessDenied = ctx =>
    {
        // Fuldt logget ind men mangler rettighed → 403. Kun adgangskode → opsæt 2FA.
        if (ctx.HttpContext.User.HasClaim("amr", "mfa")) ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        else ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});
builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorUserIdScheme, o =>
{
    o.Cookie.Name = "__Host-Tumle-2fa";
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
});
builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "__Host-Tumle-AF";
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
});

// Alt kræver login MED to-faktor og en rolle, medmindre en side udtrykkeligt siger andet.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(Policies.RoleAtLeast(Roles.Borler))
    .AddPolicy(Policies.DiktatorOrHigher, Policies.RoleAtLeast(Roles.Diktator))
    .AddPolicy(Policies.Administrator, Policies.RoleAtLeast(Roles.Administrator))
    .AddPolicy(Policies.PasswordOnly, p => p.RequireAuthenticatedUser());

// Begræns POST-forsøg pr. IP på konto-siderne (brute force / password spraying / mailspam).
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var permits = builder.Configuration.GetValue("Security:AuthPostsPerMinute", 30);
    o.AddPolicy("auth", ctx => HttpMethods.IsPost(ctx.Request.Method)
        ? RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
        : RateLimitPartition.GetNoLimiter("get"));
    // Chat: højst 20 beskeder pr. minut pr. bruger.
    o.AddPolicy("chat", ctx => HttpMethods.IsPost(ctx.Request.Method)
        ? RateLimitPartition.GetFixedWindowLimiter(
            ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
        : RateLimitPartition.GetNoLimiter("chat-get"));
});

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AddFolderApplicationModelConvention("/Account",
        m => m.EndpointMetadata.Add(new EnableRateLimitingAttribute("auth")));
});
builder.Services.Configure<CookieTempDataProviderOptions>(o =>
{
    o.Cookie.Name = "__Host-Tumle-Msg";
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddHsts(o => o.MaxAge = TimeSpan.FromDays(365));

var app = builder.Build();
app.Logger.LogInformation("Datamappe: {DataDirectory}", appData);
app.Logger.LogInformation("SMTP: {Smtp}", app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<SmtpOptions>>().Value);
var dkFilter = app.Services.GetRequiredService<CountryIpFilter>();
var securityOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<SecurityOptions>>().Value;
if (!securityOptions.DenmarkOnly)
    app.Logger.LogWarning("Landefilter er slået FRA (Security:DenmarkOnly = false). Alle lande har adgang.");
else if (dkFilter.RangeCount == 0)
    app.Logger.LogError("Listen over danske IP-adresser mangler ({Path}). ALLE forespørgsler udefra afvises.", dkRangesPath);
else
    app.Logger.LogInformation("Landefilter: kun Danmark ({Count} adresseintervaller).", dkFilter.RangeCount);
if (app.Environment.IsProduction() && DataDirectory.IsInside(appData, app.Environment.ContentRootPath))
    app.Logger.LogWarning("Datamappen ligger inde i web-mappen og kan blive slettet ved næste Git-import. Opret mappen ../{Folder}.", DataDirectory.DefaultFolderName);

await StartupTasks.InitializeAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");
app.UseHttpsRedirection();

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["Content-Security-Policy"] =
        "default-src 'none'; script-src 'self'; connect-src 'self'; style-src 'self'; font-src 'self'; img-src 'self' data:; " +
        "form-action 'self'; frame-ancestors 'none'; base-uri 'none'; upgrade-insecure-requests";
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=(), interest-cohort=()";
    h["Cross-Origin-Opener-Policy"] = "same-origin";
    h["Cross-Origin-Resource-Policy"] = "same-origin";
    ctx.Response.OnStarting(() =>
    {
        if (!ctx.Response.Headers.ContainsKey("Cache-Control"))
            ctx.Response.Headers.CacheControl = "no-store";
        return Task.CompletedTask;
    });
    await next();
});

// Forespørgsler må højst være 1 MB. Billed-upload hæver grænsen for sin egen side ([RequestSizeLimit]).
// Virker både på IIS (hvor web.config tillader 200 MB) og Kestrel.
app.Use((ctx, next) =>
{
    if (ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        limit.MaxRequestBodySize = 1024 * 1024;
    return next(ctx);
});

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = c => c.Context.Response.Headers.CacheControl = "public, max-age=86400"
});

// Kun danske IP-adresser. Efter statiske filer, så afvisningssiden kan hente sit stylesheet.
app.UseMiddleware<DenmarkOnlyMiddleware>();

app.UseRouting();
app.UseAuthentication();
// Hvem er online: hver forespørgsel fra en fuldt logget ind bruger (med to-faktor) opdaterer "sidst set".
app.Use((ctx, next) =>
{
    if (ctx.User.HasClaim("amr", "mfa") && ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value is { } userId)
        ctx.RequestServices.GetRequiredService<PresenceTracker>().Touch(userId, ctx.User.Identity?.Name ?? "");
    return next(ctx);
});
app.UseRateLimiter();   // efter login, så chat-grænsen gælder pr. bruger
app.UseAuthorization();

app.MapRazorPages();

app.Run();

/// <summary>Gør Program synlig for integrationstests (WebApplicationFactory).</summary>
public partial class Program;
