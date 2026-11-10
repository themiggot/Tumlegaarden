using System.Collections.Concurrent;
using System.Globalization;

namespace Tumlegaarden.Web.Services;

/// <summary>
/// Holder styr på hvem der er online. Hver forespørgsel fra en fuldt logget ind bruger opdaterer
/// "sidst set". Gemmes kun i hukommelsen (ingen database) og nulstilles, når appen genstarter.
/// Chatboksen spørger serveren mindst hvert 30. sekund, så længe siden er åben og synlig,
/// og holder dermed brugeren online.
/// </summary>
public sealed class PresenceTracker(TimeProvider time)
{
    /// <summary>Så længe efter sidste forespørgsel regnes man for online.</summary>
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(2);

    public sealed record Entry(string UserId, string Email, DateTime LastSeenUtc, bool SignedOut = false)
    {
        public string DisplayName => Email.Split('@')[0];
    }

    private readonly ConcurrentDictionary<string, Entry> _seen = new();

    /// <summary>Låste/slettede brugere. Deres session kan leve op til et minut endnu; de skal ikke vises som online imens.</summary>
    private readonly ConcurrentDictionary<string, bool> _blocked = new();

    public void Touch(string userId, string email)
    {
        if (_blocked.ContainsKey(userId)) return;
        _seen[userId] = new Entry(userId, email, time.GetUtcNow().UtcDateTime);
    }

    /// <summary>Log ud: offline med det samme, men "sidst set" bevares.</summary>
    public void SignedOut(string userId) => MarkOffline(userId);

    /// <summary>Låst: offline med det samme og bliver ikke online igen, før brugeren låses op.</summary>
    public void Block(string userId)
    {
        _blocked[userId] = true;
        MarkOffline(userId);
    }

    public void Unblock(string userId) => _blocked.TryRemove(userId, out _);

    /// <summary>Slettet bruger: glemmes helt.</summary>
    public void Forget(string userId)
    {
        _blocked[userId] = true;
        _seen.TryRemove(userId, out _);
    }

    private void MarkOffline(string userId)
    {
        if (_seen.TryGetValue(userId, out var e)) _seen[userId] = e with { SignedOut = true };
    }

    public bool IsOnline(string userId) =>
        _seen.TryGetValue(userId, out var e) && !e.SignedOut && time.GetUtcNow().UtcDateTime - e.LastSeenUtc <= OnlineWindow;

    public DateTime? LastSeenUtc(string userId) => _seen.TryGetValue(userId, out var e) ? e.LastSeenUtc : null;

    /// <summary>Brugere der er online lige nu, sorteret efter navn.</summary>
    public IReadOnlyList<Entry> Online()
    {
        var cutoff = time.GetUtcNow().UtcDateTime - OnlineWindow;
        return _seen.Values.Where(e => !e.SignedOut && e.LastSeenUtc >= cutoff).OrderBy(e => e.Email, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>"Online", "Sidst set 14:32", "Sidst set i går 21:05", "Sidst set 3. nov." eller "Ikke set siden genstart".</summary>
    public string StatusText(string userId)
    {
        if (IsOnline(userId)) return "Online";
        if (LastSeenUtc(userId) is not { } utc) return "Ikke set siden genstart";
        var local = TimeZoneInfo.ConvertTime(new DateTimeOffset(utc, TimeSpan.Zero), DanishTime.Zone).DateTime;
        var today = DanishTime.Today(time);
        var day = DateOnly.FromDateTime(local);
        var clock = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        return day == today ? $"Sidst set {clock}"
            : day == today.AddDays(-1) ? $"Sidst set i går {clock}"
            : $"Sidst set {local.ToString("d. MMM", DanishTime.Culture)}";
    }
}
