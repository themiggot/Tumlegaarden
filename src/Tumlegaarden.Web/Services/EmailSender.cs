using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Tumlegaarden.Web.Services;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Tumlegaarden";

    /// <summary>
    /// Kryptering: "StartTls" (kræver TLS, port 587), "SslOnConnect" (port 465),
    /// "StartTlsWhenAvailable" (TLS hvis serveren tilbyder det) eller "None".
    /// Tom = SslOnConnect på port 465, ellers StartTls.
    /// </summary>
    public string Security { get; set; } = "";

    public SecureSocketOptions SocketOptions => Security.Trim().ToLowerInvariant() switch
    {
        "starttls" => SecureSocketOptions.StartTls,
        "sslonconnect" or "ssl" => SecureSocketOptions.SslOnConnect,
        "starttlswhenavailable" or "auto" => SecureSocketOptions.StartTlsWhenAvailable,
        "none" => SecureSocketOptions.None,
        "" => Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls,
        _ => throw new InvalidOperationException($"Ukendt Smtp:Security '{Security}'.")
    };

    /// <summary>Til loggen – uden adgangskode.</summary>
    public override string ToString() =>
        $"{Host}:{Port} ({SocketOptions}), login: {(string.IsNullOrEmpty(UserName) ? "intet" : UserName)}, " +
        $"adgangskode: {(string.IsNullOrEmpty(Password) ? "mangler" : "sat")}, afsender: {FromAddress}";
}

public interface IAppEmailSender
{
    Task SendAsync(string to, string subject, string text);
}

/// <summary>Sender mail via SMTP (hos Simply.com: websmtp.simply.com, som er beregnet til mail fra webhotellet).</summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, IHostEnvironment env, ILogger<SmtpEmailSender> logger) : IAppEmailSender
{
    public async Task SendAsync(string to, string subject, string text)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.Host))
        {
            if (env.IsDevelopment())
            {
                logger.LogWarning("SMTP ikke konfigureret (kun udvikling). Mail til {To}: {Subject}\n{Text}", to, subject, text);
                return;
            }
            throw new InvalidOperationException("Smtp:Host er ikke konfigureret.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(o.FromName, o.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = text };

        using var client = new SmtpClient();
        client.Timeout = 20_000;
        await client.ConnectAsync(o.Host, o.Port, o.SocketOptions);
        if (!string.IsNullOrEmpty(o.UserName))
        {
            await client.AuthenticateAsync(o.UserName, o.Password);
        }
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
        logger.LogInformation("Mail sendt til {To} via {Host}:{Port}.", to, o.Host, o.Port);
    }
}
