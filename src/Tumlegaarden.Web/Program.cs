using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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
var appData = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(appData);

// Database: MySQL (Simply.com), SQL Server eller – hvis intet er sat op – en SQLite-fil i App_Data.
var mySql = builder.Configuration.GetConnectionString("MySql");
var sqlServer = builder.Configuration.GetConnectionString("SqlServer");
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (!string.IsNullOrWhiteSpace(mySql)) o.UseMySQL(mySql);
    else if (!string.IsNullOrWhiteSpace(sqlServer)) o.UseSqlServer(sqlServer);
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
    .AddPolicy(Policies.BorlerOrHigher, Policies.RoleAtLeast(Roles.Borler))
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
});

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AddFolderApplicationModelConvention("/Account",
        m => m.EndpointMetadata.Add(new EnableRateLimitingAttribute("auth")));
    o.Conventions.AuthorizeFolder("/Admin", Policies.Administrator);
});
builder.Services.Configure<CookieTempDataProviderOptions>(o =>
{
    o.Cookie.Name = "__Host-Tumle-Msg";
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddHsts(o => o.MaxAge = TimeSpan.FromDays(365));

var app = builder.Build();

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
        "default-src 'none'; style-src 'self'; font-src 'self'; img-src 'self' data:; " +
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

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = c => c.Context.Response.Headers.CacheControl = "public, max-age=86400"
});

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
