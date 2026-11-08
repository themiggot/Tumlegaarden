using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Security;

namespace Tumlegaarden.Web.Services;

public sealed class InvitationService(AppDbContext db, TimeProvider time)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(72);

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    /// <summary>Opretter en invitation og returnerer det hemmelige token (vises/sendes kun én gang).</summary>
    public async Task<string> CreateAsync(string email, string role, string? createdBy)
    {
        if (!Roles.IsValid(role)) throw new ArgumentException("Ukendt rolle.", nameof(role));

        var normalized = email.Trim().ToUpperInvariant();
        // Kun én gyldig invitation pr. adresse ad gangen.
        var previous = await db.Invitations
            .Where(i => i.NormalizedEmail == normalized && i.UsedUtc == null && i.RevokedUtc == null)
            .ToListAsync();
        foreach (var p in previous) p.RevokedUtc = Now;

        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        db.Invitations.Add(new Invitation
        {
            Email = email.Trim(),
            NormalizedEmail = normalized,
            Role = role,
            TokenHash = Hash(token),
            CreatedBy = createdBy,
            CreatedUtc = Now,
            ExpiresUtc = Now.Add(Lifetime)
        });
        await db.SaveChangesAsync();
        return token;
    }

    public async Task<Invitation?> FindValidAsync(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 100) return null;
        var hash = Hash(token);
        var invitation = await db.Invitations.SingleOrDefaultAsync(i => i.TokenHash == hash);
        return invitation?.IsPending(Now) == true ? invitation : null;
    }

    public async Task<List<Invitation>> PendingAsync()
    {
        var open = await db.Invitations.Where(i => i.UsedUtc == null && i.RevokedUtc == null).ToListAsync();
        return open.Where(i => i.IsPending(Now)).OrderByDescending(i => i.CreatedUtc).ToList();
    }

    public async Task<bool> AnyPendingAsync() => (await PendingAsync()).Count > 0;

    public async Task MarkUsedAsync(Invitation invitation)
    {
        invitation.UsedUtc = Now;
        await db.SaveChangesAsync();
    }

    public async Task RevokeAsync(int id)
    {
        var invitation = await db.Invitations.FindAsync(id);
        if (invitation is { UsedUtc: null, RevokedUtc: null })
        {
            invitation.RevokedUtc = Now;
            await db.SaveChangesAsync();
        }
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
