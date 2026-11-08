using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Tumlegaarden.Web.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<CalendarEvent> Events => Set<CalendarEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // MySQL kan ikke indeksere tekstkolonner uden længde. Giv alle nøgle- og
        // indekskolonner uden længde maks. 255 tegn (rigeligt til GUID'er og navne).
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            var keyed = entity.GetKeys().SelectMany(k => k.Properties)
                .Concat(entity.GetForeignKeys().SelectMany(f => f.Properties))
                .Concat(entity.GetIndexes().SelectMany(i => i.Properties));
            foreach (var property in keyed.Distinct())
            {
                if (property.ClrType == typeof(string) && property.GetMaxLength() is null)
                    property.SetMaxLength(255);
            }
        }

        builder.Entity<CalendarEvent>(e =>
        {
            e.ToTable("Events");
            e.HasIndex(x => x.StartsAt);
            e.Property(x => x.Title).HasMaxLength(CalendarEvent.TitleMaxLength).IsRequired();
            e.Property(x => x.Body).HasMaxLength(CalendarEvent.BodyMaxLength).IsRequired();
            e.Property(x => x.CreatedById).HasMaxLength(255);
            e.Property(x => x.CreatedByEmail).HasMaxLength(256);
        });

        builder.Entity<Invitation>(e =>
        {
            e.HasIndex(i => i.TokenHash).IsUnique();
            e.HasIndex(i => i.NormalizedEmail);
            e.Property(i => i.Email).HasMaxLength(256);
            e.Property(i => i.NormalizedEmail).HasMaxLength(256);
            e.Property(i => i.Role).HasMaxLength(64);
            e.Property(i => i.TokenHash).HasMaxLength(64);
            e.Property(i => i.CreatedBy).HasMaxLength(256);
        });
    }
}
