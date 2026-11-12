using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Tests;

/// <summary>GDPR: e-mail fjernes fra slettede brugeres indhold, og gamle logfiler slettes.</summary>
public sealed class DataRetentionTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static IdentityUser User(string name) => new()
        { Id = name, UserName = $"{name}@tumlegaarden.dk", NormalizedUserName = $"{name}@TUMLEGAARDEN.DK".ToUpperInvariant(),
          Email = $"{name}@tumlegaarden.dk", NormalizedEmail = $"{name}@tumlegaarden.dk".ToUpperInvariant() };

    private void AddContent(string userId)
    {
        var email = $"{userId}@tumlegaarden.dk";
        var album = new Album { Title = "Fest " + userId, CreatedById = userId, CreatedByEmail = email };
        _db.Albums.Add(album);
        _db.Photos.Add(new Photo { Album = album, StorageName = userId + ".jpg", UploadedById = userId, UploadedByEmail = email });
        _db.Events.Add(new CalendarEvent { Title = "Høst " + userId, CreatedById = userId, CreatedByEmail = email });
        var room = new ChatRoom { Name = "Rum " + userId, CreatedByEmail = email };
        _db.ChatRooms.Add(room);
        _db.SaveChanges();
        _db.ChatMessages.Add(new ChatMessage { RoomId = room.Id, UserId = userId, UserEmail = email, Text = "hej", SentUtc = Now });
        _db.Invitations.Add(new Invitation { Email = email, NormalizedEmail = email.ToUpperInvariant(), Role = "Børler", TokenHash = "t-" + userId,
            CreatedBy = "admin@tumlegaarden.dk", CreatedUtc = Now.AddDays(-10), ExpiresUtc = Now.AddDays(-3), UsedUtc = Now.AddDays(-9) });
    }

    [Fact]
    public async Task DeletedUser_EmailIsRemovedEverywhere_OthersAreUntouched()
    {
        _db.Users.AddRange(User("admin"), User("bo"), User("ida"));
        AddContent("bo");
        AddContent("ida");
        // En afventende invitation til en ny adresse skal blive stående.
        _db.Invitations.Add(new Invitation { Email = "ny@tumlegaarden.dk", NormalizedEmail = "NY@TUMLEGAARDEN.DK", Role = "Børler", TokenHash = "pending",
            CreatedBy = "bo@tumlegaarden.dk", CreatedUtc = Now, ExpiresUtc = Now.AddDays(3) });
        await _db.SaveChangesAsync();

        _db.Users.Remove(await _db.Users.FindAsync("bo") ?? throw new InvalidOperationException());
        await _db.SaveChangesAsync();
        Assert.True(await UserDataEraser.AnonymizeDeletedUsersAsync(_db, Now) > 0);
        _db.ChangeTracker.Clear();

        var all = string.Join("|",
            (await _db.Albums.Select(a => a.CreatedByEmail).ToListAsync())
            .Concat(await _db.Photos.Select(p => p.UploadedByEmail).ToListAsync())
            .Concat(await _db.Events.Select(e => e.CreatedByEmail).ToListAsync())
            .Concat(await _db.ChatRooms.Select(r => r.CreatedByEmail).ToListAsync())
            .Concat(await _db.ChatMessages.Select(m => m.UserEmail).ToListAsync())
            .Concat(await _db.Invitations.Select(i => i.Email + " " + i.CreatedBy).ToListAsync()));
        Assert.DoesNotContain("bo@", all);
        Assert.Contains("ida@tumlegaarden.dk", all);
        Assert.Equal(UserDataEraser.DeletedUser, (await _db.Albums.SingleAsync(a => a.Title == "Fest bo")).CreatedByEmail);
        Assert.Equal(2, await _db.Albums.CountAsync());                                   // indholdet bliver stående
        Assert.False(await _db.Invitations.AnyAsync(i => i.TokenHash == "t-bo"));          // brugt invitation til slettet adresse
        Assert.True(await _db.Invitations.AnyAsync(i => i.TokenHash == "t-ida"));
        Assert.Equal(UserDataEraser.DeletedUser, (await _db.Invitations.SingleAsync(i => i.TokenHash == "pending")).CreatedBy);

        Assert.Equal(0, await UserDataEraser.AnonymizeDeletedUsersAsync(_db, Now));         // kan gentages uden ændringer
    }

    [Fact]
    public void LogRetention_DeletesOnlyOldLogFiles()
    {
        var dir = Directory.CreateTempSubdirectory("tumle-logs-").FullName;
        try
        {
            string Make(string name, int daysOld)
            {
                var path = Path.Combine(dir, name);
                File.WriteAllText(path, "x");
                File.SetLastWriteTimeUtc(path, Now.AddDays(-daysOld));
                return path;
            }
            var old = Make("stdout_20260901_1.log", 40);
            var fresh = Make("stdout_20261110_2.log", 2);
            var edge = Make("stdout_20261014_3.log", 29);
            var other = Make("vigtig.txt", 400);

            var retention = new LogRetention(dir, 30);
            Assert.Equal(1, retention.DeleteOld(Now));
            Assert.False(File.Exists(old));
            Assert.True(File.Exists(fresh) && File.Exists(edge) && File.Exists(other));
            Assert.Equal(0, new LogRetention(Path.Combine(dir, "findes-ikke"), 30).DeleteOld(Now));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
