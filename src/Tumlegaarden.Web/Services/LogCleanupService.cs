namespace Tumlegaarden.Web.Services;

/// <summary>
/// Sletter logfiler ældre end <c>Logs:RetentionDays</c> (standard 30 dage) fra logmappen
/// (<c>Logs:Directory</c>, standard ../tumlegaarden-logs, hvor IIS skriver stdout-loggen).
/// Loggen indeholder IP-adresser og e-mailadresser, så den må ikke gemmes for evigt (GDPR art. 5).
/// Kører ved opstart og derefter hver 6. time. Kun *.log-filer direkte i mappen røres.
/// </summary>
public sealed class LogCleanupService(LogRetention retention, ILogger<LogCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                var deleted = retention.DeleteOld(DateTime.UtcNow);
                if (deleted > 0) logger.LogInformation("Slettede {Count} logfiler ældre end {Days} dage.", deleted, retention.Days);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Oprydning af gamle logfiler fejlede.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public sealed class LogRetention(string directory, int days)
{
    public const int DefaultDays = 30;
    public const string DefaultFolderName = "tumlegaarden-logs";

    public string Directory { get; } = directory;
    public int Days { get; } = Math.Max(1, days);

    public static LogRetention FromConfiguration(IConfiguration config, IHostEnvironment env) => new(
        config["Logs:Directory"] is { Length: > 0 } dir ? Path.GetFullPath(dir, env.ContentRootPath)
            : Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", DefaultFolderName)),
        config.GetValue("Logs:RetentionDays", DefaultDays));

    /// <summary>Sletter *.log-filer, der ikke er skrevet i i <see cref="Days"/> dage. Filer i brug springes over.</summary>
    public int DeleteOld(DateTime nowUtc)
    {
        if (!System.IO.Directory.Exists(Directory)) return 0;
        var cutoff = nowUtc.AddDays(-Days);
        var deleted = 0;
        foreach (var file in new DirectoryInfo(Directory).EnumerateFiles("*.log", SearchOption.TopDirectoryOnly))
        {
            if (file.LastWriteTimeUtc >= cutoff) continue;
            try
            {
                file.Delete();
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Fx den aktuelle stdout-log, som IIS har åben. Prøves igen næste gang.
            }
        }
        return deleted;
    }
}
