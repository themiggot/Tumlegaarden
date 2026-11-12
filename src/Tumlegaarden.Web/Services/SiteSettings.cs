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

    public const string GameSecretKey = "Game.Secret";
    public const string AfterDarkSecretKey = "Game.AfterDark.Secret";

    /// <summary>
    /// Hemmelig nøgle til Ordjagt (bestemmer ordenes rækkefølge). Laves første gang og gemmes i databasen,
    /// så dagens ord ikke kan regnes ud fra det offentlige repo.
    /// </summary>
    public Task<byte[]> GetOrCreateGameSecretAsync() => GetOrCreateGameSecretAsync(GameSecretKey);

    /// <summary>Samme slags nøgle, men med eget navn pr. spiltype (fx After Dark).</summary>
    public async Task<byte[]> GetOrCreateGameSecretAsync(string key)
    {
        var setting = await db.Settings.FindAsync(key);
        if (setting is not null) return Convert.FromBase64String(setting.Value);

        var secret = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        db.Settings.Add(new SiteSetting { Key = key, Value = Convert.ToBase64String(secret), UpdatedUtc = time.GetUtcNow().UtcDateTime, UpdatedBy = "system" });
        try
        {
            await db.SaveChangesAsync();
            return secret;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            // En anden forespørgsel nåede at oprette nøglen samtidig – brug dens.
            db.ChangeTracker.Clear();
            return Convert.FromBase64String((await db.Settings.FindAsync(key))!.Value);
        }
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
