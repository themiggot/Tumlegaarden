namespace Tumlegaarden.Web.Services;

/// <summary>Finder mappen til database-fil, krypteringsnøgler og appsettings.Production.json.</summary>
public static class DataDirectory
{
    public const string DefaultFolderName = "tumlegaarden-data";

    /// <summary>
    /// Rækkefølge: App:DataPath fra konfiguration/miljøvariabel, derefter mappen
    /// "tumlegaarden-data" ved siden af web-mappen (hvis den findes), ellers App_Data i appen.
    /// </summary>
    public static string Resolve(IConfiguration configuration, IHostEnvironment env)
    {
        var configured = configuration["App:DataPath"];
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured, env.ContentRootPath);

        var sibling = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", DefaultFolderName));
        if (Directory.Exists(sibling)) return sibling;

        return Path.Combine(env.ContentRootPath, "App_Data");
    }

    public static bool IsInside(string path, string root)
    {
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var parent = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
    }
}
