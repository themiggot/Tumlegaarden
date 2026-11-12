using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;

namespace Tumlegaarden.Web.Services;

/// <summary>
/// Fjerner e-mailadresser på brugere, der er slettet (GDPR: ret til sletning).
/// Kontoen, svar, spil og chatmedlemskaber slettes af databasen (cascade). Indhold, som andre
/// også bruger – begivenheder, albums, billeder, quizzer og chatrum – bliver stående, men e-mailen
/// erstattes af <see cref="DeletedUser"/>. Færdige invitationer til slettede adresser fjernes.
/// Kører efter hver sletning og ved opstart, så også brugere slettet før denne kode ryddes op.
/// </summary>
public static class UserDataEraser
{
    public const string DeletedUser = "slettet bruger";

    public static async Task<int> AnonymizeDeletedUsersAsync(AppDbContext db, DateTime nowUtc, CancellationToken ct = default)
    {
        var users = db.Users;
        var changed = 0;

        changed += await db.Albums.Where(a => a.CreatedByEmail != DeletedUser && !users.Any(u => u.Id == a.CreatedById))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.CreatedByEmail, DeletedUser), ct);
        changed += await db.Photos.Where(p => p.UploadedByEmail != DeletedUser && !users.Any(u => u.Id == p.UploadedById))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.UploadedByEmail, DeletedUser), ct);
        changed += await db.Events.Where(e => e.CreatedByEmail != DeletedUser && !users.Any(u => u.Id == e.CreatedById))
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.CreatedByEmail, DeletedUser), ct);
        changed += await db.Quizzes.Where(q => q.CreatedByEmail != DeletedUser && !users.Any(u => u.Id == q.CreatedById))
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.CreatedByEmail, DeletedUser), ct);
        changed += await db.ChatMessages.Where(m => m.UserEmail != DeletedUser && !users.Any(u => u.Id == m.UserId))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.UserEmail, DeletedUser), ct);
        // Chatrum gemmer kun opretterens e-mail (og "system" for det globale rum).
        changed += await db.ChatRooms.Where(r => r.CreatedByEmail != "" && r.CreatedByEmail != DeletedUser && r.CreatedByEmail != "system"
                && !users.Any(u => u.NormalizedEmail == r.CreatedByEmail.ToUpper()))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.CreatedByEmail, DeletedUser), ct);

        // Invitationer: afsluttede invitationer til adresser uden konto slettes; inviterende, der er slettet, anonymiseres.
        changed += await db.Invitations.Where(i => (i.UsedUtc != null || i.RevokedUtc != null || i.ExpiresUtc <= nowUtc)
                && !users.Any(u => u.NormalizedEmail == i.NormalizedEmail))
            .ExecuteDeleteAsync(ct);
        changed += await db.Invitations.Where(i => i.CreatedBy != null && i.CreatedBy != "system" && i.CreatedBy != DeletedUser
                && !users.Any(u => u.UserName == i.CreatedBy))
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.CreatedBy, DeletedUser), ct);
        return changed;
    }
}
