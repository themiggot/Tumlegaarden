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
}

public interface IAppEmailSender
{
    Task SendAsync(string to, string subject, string text);
}

/// <summary>Sender mail via Simply.com's SMTP med påkrævet TLS (STARTTLS på 587, SSL på 465).</summary>
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
        var socket = o.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(o.Host, o.Port, socket);
        if (!string.IsNullOrEmpty(o.UserName))
        {
            await client.AuthenticateAsync(o.UserName, o.Password);
        }
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
