namespace Tumlegaarden.Web.Services;

/// <summary>
/// Sletter chatbeskeder ældre end 24 timer – ved opstart og derefter hvert kvarter.
/// På et webhotel kan appen gå i dvale, så kører oprydningen ikke; derfor filtrerer
/// ChatService også gamle beskeder fra, så de aldrig vises.
/// </summary>
public sealed class ChatCleanupService(IServiceScopeFactory scopes, ILogger<ChatCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var deleted = await scope.ServiceProvider.GetRequiredService<ChatService>().DeleteExpiredAsync(stoppingToken);
                if (deleted > 0) logger.LogInformation("Slettede {Count} chatbeskeder ældre end 24 timer.", deleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Oprydning af gamle chatbeskeder fejlede.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
