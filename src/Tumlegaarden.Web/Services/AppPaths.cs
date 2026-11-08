namespace Tumlegaarden.Web.Services;

/// <summary>Stier til datamappen (uden for web-mappen).</summary>
public sealed class AppPaths(string dataDirectory)
{
    public string DataDirectory { get; } = dataDirectory;
    public string Albums => Path.Combine(DataDirectory, "albums");
    public string Temp => Path.Combine(DataDirectory, "tmp");
}
