using System.Net;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;

namespace Tumlegaarden.Web.Security;

/// <summary>
/// Afviser alle forespørgsler fra IP-adresser uden for Danmark (HTTP 403) – også for brugere der allerede er logget ind.
/// Lokale/private adresser (udvikling) og adresser i Security:AllowedIpRanges slipper igennem.
/// Mangler listen over danske adresser, afvises alle (fail closed).
/// </summary>
public sealed class DenmarkOnlyMiddleware(
    RequestDelegate next,
    CountryIpFilter denmark,
    IOptions<SecurityOptions> options,
    IHostEnvironment env,
    ILogger<DenmarkOnlyMiddleware> logger)
{
    private static readonly IPNetwork[] PrivateNetworks =
    [
        IPNetwork.Parse("10.0.0.0/8"), IPNetwork.Parse("172.16.0.0/12"), IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("169.254.0.0/16"), IPNetwork.Parse("fc00::/7"), IPNetwork.Parse("fe80::/10")
    ];

    private readonly IPNetwork[] _allowed = (options.Value.AllowedIpRanges ?? [])
        .Select(r => r.Contains('/') ? IPNetwork.Parse(r.Trim()) : new IPNetwork(IPAddress.Parse(r.Trim()), IPAddress.Parse(r.Trim()).AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128))
        .ToArray();

    private static int _privateWarningLogged;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!options.Value.DenmarkOnly || IsAllowed(context.Connection.RemoteIpAddress))
        {
            await next(context);
            return;
        }

        logger.LogInformation("Afviste {Method} {Path} fra {Ip}: ikke en dansk IP-adresse.",
            context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(BlockedPage(context.Connection.RemoteIpAddress));
    }

    public bool IsAllowed(IPAddress? ip)
    {
        if (ip is null) return false;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip) || PrivateNetworks.Any(n => n.Contains(ip)))
        {
            if (!env.IsDevelopment() && Interlocked.Exchange(ref _privateWarningLogged, 1) == 0)
            {
                logger.LogWarning("Forespørgsel fra lokal/privat IP {Ip}. Står sitet bag en proxy, kan landefilteret ikke se den rigtige adresse.", ip);
            }
            return true;
        }

        return _allowed.Any(n => n.Contains(ip)) || denmark.Contains(ip);
    }

    private static string BlockedPage(IPAddress? ip) => $$"""
        <!DOCTYPE html>
        <html lang="da">
        <head>
        <meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <meta name="robots" content="noindex, nofollow" />
        <title>Ingen adgang · Tumlegaarden</title>
        <link rel="stylesheet" href="/css/site.css" />
        </head>
        <body>
        <header class="topbar"><h1 class="logo"><a href="/">Tumlegaarden</a></h1></header>
        <main class="stage">
        <section class="window">
        <h2 class="window-title">Ingen adgang</h2>
        <div class="window-body">
        <p>Tumlegaarden kan kun bruges fra danske IP-adresser.</p>
        <p class="hint">Er du i Danmark, så slå VPN eller iCloud Privat Relay fra og prøv igen. Din adresse: {{HtmlEncoder.Default.Encode(ip?.ToString() ?? "ukendt")}}</p>
        </div>
        </section>
        </main>
        </body>
        </html>
        """;
}
