namespace Tumlegaarden.Web.Data;

/// <summary>
/// Engangsinvitation. Kun SHA-256-hashen af tokenet gemmes, så et læk af databasen
/// ikke giver brugbare invitationslinks.
/// </summary>
public sealed class Invitation
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string Role { get; set; }
    public required string TokenHash { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime? UsedUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }

    public bool IsPending(DateTime nowUtc) => UsedUtc is null && RevokedUtc is null && ExpiresUtc > nowUtc;
}
