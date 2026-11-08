using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Security;

namespace Tumlegaarden.Web.Services;

/// <summary>
/// Adgang til chat: alle kan bruge det globale rum. Andre rum kun medlemmer –
/// diktatorer og administratorer kan se og administrere alle rum.
/// </summary>
public sealed class ChatService(AppDbContext db, UserManager<IdentityUser> userManager, TimeProvider time)
{
    public const string GlobalRoomName = "Alle";

    /// <summary>Chatbeskeder slettes, når de er ældre end dette – og vises aldrig, selv hvis oprydningen ikke har kørt.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    private DateTime Cutoff => time.GetUtcNow().UtcDateTime - Retention;

    public sealed record MessageDto(long Id, string User, string Text, string Time, bool Mine);
    public sealed record RoomDto(int Id, string Name, bool IsGlobal, long LatestId);

    public async Task<List<ChatRoom>> RoomsForAsync(ClaimsPrincipal user)
    {
        var userId = userManager.GetUserId(user);
        var query = db.ChatRooms.AsNoTracking();
        if (!Roles.IsModerator(user))
            query = query.Where(r => r.IsGlobal || r.Members.Any(m => m.UserId == userId));
        return await query.OrderByDescending(r => r.IsGlobal).ThenBy(r => r.Name).ToListAsync();
    }

    public async Task<List<RoomDto>> RoomSummariesAsync(ClaimsPrincipal user)
    {
        var rooms = await RoomsForAsync(user);
        var ids = rooms.Select(r => r.Id).ToList();
        var cutoff = Cutoff;
        var latest = await db.ChatMessages.Where(m => ids.Contains(m.RoomId) && m.SentUtc >= cutoff)
            .GroupBy(m => m.RoomId)
            .Select(g => new { RoomId = g.Key, Latest = g.Max(m => m.Id) })
            .ToDictionaryAsync(x => x.RoomId, x => x.Latest);
        return rooms.Select(r => new RoomDto(r.Id, r.Name, r.IsGlobal, latest.GetValueOrDefault(r.Id))).ToList();
    }

    public async Task<ChatRoom?> RoomForAsync(ClaimsPrincipal user, int roomId) =>
        (await RoomsForAsync(user)).FirstOrDefault(r => r.Id == roomId);

    /// <summary>Beskeder efter <paramref name="afterId"/>, eller de seneste <paramref name="take"/> hvis afterId er 0.</summary>
    public async Task<List<MessageDto>> MessagesAsync(ClaimsPrincipal user, int roomId, long afterId, int take = 50)
    {
        var cutoff = Cutoff;
        var query = db.ChatMessages.AsNoTracking().Where(m => m.RoomId == roomId && m.SentUtc >= cutoff);
        var messages = afterId > 0
            ? await query.Where(m => m.Id > afterId).OrderBy(m => m.Id).Take(take).ToListAsync()
            : (await query.OrderByDescending(m => m.Id).Take(take).ToListAsync()).OrderBy(m => m.Id).ToList();
        var userId = userManager.GetUserId(user);
        return messages.Select(m => ToDto(m, userId)).ToList();
    }

    public async Task<MessageDto> SendAsync(ClaimsPrincipal principal, int roomId, string text)
    {
        var user = await userManager.GetUserAsync(principal) ?? throw new InvalidOperationException("Ingen bruger.");
        var message = new ChatMessage
        {
            RoomId = roomId,
            UserId = user.Id,
            UserEmail = user.Email ?? "",
            Text = text,
            SentUtc = time.GetUtcNow().UtcDateTime
        };
        db.ChatMessages.Add(message);
        await db.SaveChangesAsync();
        return ToDto(message, user.Id);
    }

    /// <summary>Normaliserer en besked. Null hvis den er tom eller for lang.</summary>
    public static string? Clean(string? text)
    {
        var t = (text ?? "").Replace("\r\n", "\n").Trim();
        return t.Length is 0 or > ChatMessage.TextMaxLength ? null : t;
    }

    /// <summary>Sletter beskeder ældre end <see cref="Retention"/>. Returnerer antal slettede.</summary>
    public async Task<int> DeleteExpiredAsync(CancellationToken ct = default)
    {
        var cutoff = Cutoff;
        return await db.ChatMessages.Where(m => m.SentUtc < cutoff).ExecuteDeleteAsync(ct);
    }

    public async Task EnsureGlobalRoomAsync()
    {
        if (await db.ChatRooms.AnyAsync(r => r.IsGlobal)) return;
        db.ChatRooms.Add(new ChatRoom { Name = GlobalRoomName, IsGlobal = true, CreatedByEmail = "system", CreatedUtc = time.GetUtcNow().UtcDateTime });
        await db.SaveChangesAsync();
    }

    private MessageDto ToDto(ChatMessage m, string? currentUserId)
    {
        var local = TimeZoneInfo.ConvertTime(new DateTimeOffset(m.SentUtc, TimeSpan.Zero), DanishTimeZone).DateTime;
        var today = DanishTime.Today(time);
        var stamp = DateOnly.FromDateTime(local) == today
            ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
            : local.ToString("d. MMM HH:mm", DanishTime.Culture);
        return new MessageDto(m.Id, m.UserEmail.Split('@')[0], m.Text, stamp, m.UserId == currentUserId);
    }

    private static TimeZoneInfo DanishTimeZone => DanishTime.Zone;
}
