using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Tumlegaarden.Web.Data;
using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Tests;

/// <summary>
/// Opgradering af en eksisterende database (som hos Simply): nye tabeller og kolonner oprettes ved opstart,
/// uden at eksisterende data røres.
/// </summary>
public sealed class SchemaUpdaterTests : IAsyncLifetime
{
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

    private async Task<bool> TableExistsAsync(string table)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        cmd.Parameters.AddWithValue("$name", table);
        return (long)(await cmd.ExecuteScalarAsync())! == 1;
    }

    private async Task<bool> IndexExistsAsync(string index)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = $name";
        cmd.Parameters.AddWithValue("$name", index);
        return (long)(await cmd.ExecuteScalarAsync())! == 1;
    }

    [Fact]
    public async Task MissingTables_AreCreated_WithIndexes_AndKeepExistingData()
    {
        _db.Settings.Add(new SiteSetting { Key = "Bevares", Value = "ja" });
        await _db.SaveChangesAsync();
        // Som en database fra før de nyeste spil (tabeller med fremmednøgler til hinanden, slettet i rigtig rækkefølge).
        await _db.Database.ExecuteSqlRawAsync(
            "DROP TABLE \"QuizGames\"; DROP TABLE \"QuizQuestions\"; DROP TABLE \"QuizCategories\"; DROP TABLE \"Quizzes\"; DROP TABLE \"GameResults\"; DROP TABLE \"AfterDarkRounds\";");
        Assert.False(await TableExistsAsync("Quizzes"));

        await SchemaUpdater.CreateMissingTablesAsync(_db, NullLogger.Instance);

        foreach (var table in new[] { "QuizGames", "QuizQuestions", "QuizCategories", "Quizzes", "GameResults", "AfterDarkRounds" })
            Assert.True(await TableExistsAsync(table), table);
        Assert.True(await IndexExistsAsync("IX_GameResults_Game_UserId_Date"));
        Assert.True(await IndexExistsAsync("IX_QuizQuestions_QuizId_Column_Row"));
        Assert.Equal("ja", (await _db.Settings.AsNoTracking().SingleAsync(s => s.Key == "Bevares")).Value);

        await SchemaUpdater.CreateMissingTablesAsync(_db, NullLogger.Instance);   // kan køres igen uden fejl
    }

    [Fact]
    public async Task MissingNullableColumn_IsAdded()
    {
        await _db.Database.ExecuteSqlRawAsync("ALTER TABLE \"GameRounds\" DROP COLUMN \"Answer\"");

        await SchemaUpdater.AddMissingColumnsAsync(_db, NullLogger.Instance);

        _db.Users.Add(new Microsoft.AspNetCore.Identity.IdentityUser { Id = "u", UserName = "u@tumlegaarden.dk" });
        _db.GameRounds.Add(new GameRound { UserId = "u", UserEmail = "u@tumlegaarden.dk", Answer = "huset" });
        await _db.SaveChangesAsync();
        Assert.Equal("huset", (await _db.GameRounds.AsNoTracking().SingleAsync()).Answer);
    }
}
