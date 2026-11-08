using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Tumlegaarden.Web.Data;

namespace Tumlegaarden.Web.Services;

/// <summary>
/// EnsureCreated opretter kun tabeller i en tom database. Denne klasse opretter tabeller,
/// der er kommet til i en nyere version (fx Events), i en eksisterende database.
/// Den tilføjer IKKE kolonner til eksisterende tabeller – det kræver en manuel ændring.
/// </summary>
public static partial class SchemaUpdater
{
    public static async Task CreateMissingTablesAsync(AppDbContext db, ILogger log)
    {
        var sql = db.GetService<ISqlGenerationHelper>();
        var statements = StatementSplit().Split(db.Database.GenerateCreateScript())
            .Select(s => s.Trim())
            .Where(s => s.Length > 0 && !s.Equals("GO", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var table in db.Model.GetEntityTypes().Select(e => e.GetTableName()).OfType<string>().Distinct())
        {
            var quoted = sql.DelimitIdentifier(table);
            if (await TableExistsAsync(db, quoted)) continue;

            var create = statements
                .Where(s => s.StartsWith($"CREATE TABLE {quoted}", StringComparison.Ordinal)
                         || (s.StartsWith("CREATE ", StringComparison.Ordinal) && s.Contains($" ON {quoted}", StringComparison.Ordinal)))
                .ToList();
            if (create.Count == 0)
            {
                log.LogError("Tabellen {Table} mangler, men der blev ikke fundet en CREATE-sætning til den.", table);
                continue;
            }

            foreach (var statement in create) await db.Database.ExecuteSqlRawAsync(statement);
            log.LogInformation("Oprettede manglende tabel {Table}.", table);
        }
    }

    /// <summary>Tjekker direkte på forbindelsen (uden om EF), så en manglende tabel ikke logges som en fejl.</summary>
    private static async Task<bool> TableExistsAsync(AppDbContext db, string quotedTable)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT 1 FROM {quotedTable} WHERE 1 = 0";
            await command.ExecuteNonQueryAsync();
            return true;
        }
        catch (System.Data.Common.DbException)
        {
            return false;
        }
        finally
        {
            if (opened) await connection.CloseAsync();
        }
    }

    [GeneratedRegex(@";\s*\r?\n")]
    private static partial Regex StatementSplit();
}
