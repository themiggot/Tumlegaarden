using System.Globalization;

namespace Tumlegaarden.Web.Services;

/// <summary>Dansk tid og sprog til kalenderen, uanset hvilken tidszone serveren kører i.</summary>
public static class DanishTime
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("da-DK");

    public static readonly TimeZoneInfo Zone = FindZone();

    private static TimeZoneInfo FindZone()
    {
        foreach (var id in new[] { "Europe/Copenhagen", "Romance Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }

    public static DateTime Now(TimeProvider time) => TimeZoneInfo.ConvertTime(time.GetUtcNow(), Zone).DateTime;

    public static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(Now(time));
}
