using Tumlegaarden.Web.Data;

namespace Tumlegaarden.Web.Services;

/// <summary>Indstillinger der gemmes i databasen og kan ændres af administratorer under Indstillinger.</summary>
public sealed class SiteSettings(AppDbContext db, TimeProvider time)
{
    public const string MaxImageMegabytesKey = "Albums.MaxImageMegabytes";
    public const int DefaultMaxImageMegabytes = 5;

    /// <summary>Maks. størrelse pr. uploadet billede i MB (1–25, standard 5).</summary>
    public async Task<int> GetMaxImageMegabytesAsync()
    {
        var setting = await db.Settings.FindAsync(MaxImageMegabytesKey);
        return setting is not null && int.TryParse(setting.Value, out var mb) && mb is >= 1 and <= PhotoStore.MaxAllowedMegabytes
            ? mb
            : DefaultMaxImageMegabytes;
    }

    public async Task SetMaxImageMegabytesAsync(int megabytes, string? changedBy)
    {
        if (megabytes is < 1 or > PhotoStore.MaxAllowedMegabytes) throw new ArgumentOutOfRangeException(nameof(megabytes));
        var setting = await db.Settings.FindAsync(MaxImageMegabytesKey);
        if (setting is null)
        {
            setting = new SiteSetting { Key = MaxImageMegabytesKey, Value = "" };
            db.Settings.Add(setting);
        }
        setting.Value = megabytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        setting.UpdatedUtc = time.GetUtcNow().UtcDateTime;
        setting.UpdatedBy = changedBy ?? "";
        await db.SaveChangesAsync();
    }
}
