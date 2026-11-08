namespace Tumlegaarden.Web.Data;

/// <summary>En indstilling administratorer kan ændre på siden (nøgle/værdi).</summary>
public sealed class SiteSetting
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string UpdatedBy { get; set; } = "";
}
