using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Tumlegaarden.Web.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<CalendarEvent> Events => Set<CalendarEvent>();
    public DbSet<EventResponse> EventResponses => Set<EventResponse>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<SiteSetting> Settings => Set<SiteSetting>();
    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
    public DbSet<ChatRoomMember> ChatRoomMembers => Set<ChatRoomMember>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

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

        builder.Entity<EventResponse>(e =>
        {
            e.ToTable("EventResponses");
            e.HasKey(x => new { x.EventId, x.UserId });
            e.Property(x => x.UserId).HasMaxLength(255);
            e.Property(x => x.UserEmail).HasMaxLength(256);
            e.HasOne(x => x.Event).WithMany(x => x.Responses).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            // Slettes en bruger, forsvinder brugerens svar også.
            e.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Album>(e =>
        {
            e.ToTable("Albums");
            e.Property(x => x.Title).HasMaxLength(Album.TitleMaxLength).IsRequired();
            e.Property(x => x.Description).HasMaxLength(Album.DescriptionMaxLength).IsRequired();
            e.Property(x => x.CreatedById).HasMaxLength(255);
            e.Property(x => x.CreatedByEmail).HasMaxLength(256);
        });

        builder.Entity<Photo>(e =>
        {
            e.ToTable("Photos");
            e.HasOne(x => x.Album).WithMany(x => x.Photos).HasForeignKey(x => x.AlbumId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.StorageName).HasMaxLength(64);
            e.Property(x => x.OriginalName).HasMaxLength(255);
            e.Property(x => x.UploadedById).HasMaxLength(255);
            e.Property(x => x.UploadedByEmail).HasMaxLength(256);
        });

        builder.Entity<SiteSetting>(e =>
        {
            e.ToTable("Settings");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.Value).HasMaxLength(1000);
            e.Property(x => x.UpdatedBy).HasMaxLength(256);
        });

        builder.Entity<ChatRoom>(e =>
        {
            e.ToTable("ChatRooms");
            e.Property(x => x.Name).HasMaxLength(ChatRoom.NameMaxLength).IsRequired();
            e.Property(x => x.CreatedByEmail).HasMaxLength(256);
        });

        builder.Entity<ChatRoomMember>(e =>
        {
            e.ToTable("ChatRoomMembers");
            e.HasKey(x => new { x.RoomId, x.UserId });
            e.Property(x => x.UserId).HasMaxLength(255);
            e.HasOne(x => x.Room).WithMany(x => x.Members).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChatMessage>(e =>
        {
            e.ToTable("ChatMessages");
            e.HasIndex(x => new { x.RoomId, x.Id });
            e.Property(x => x.UserId).HasMaxLength(255);
            e.Property(x => x.UserEmail).HasMaxLength(256);
            e.Property(x => x.Text).HasMaxLength(ChatMessage.TextMaxLength).IsRequired();
            e.HasOne<ChatRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
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
