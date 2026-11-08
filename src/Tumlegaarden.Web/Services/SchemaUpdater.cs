using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Tumlegaarden.Web.Data;

namespace Tumlegaarden.Web.Services;

/// <summary>
/// EnsureCreated opretter kun tabeller i en tom database. Denne klasse opretter tabeller,
/// der er kommet til i en nyere version (fx Events), i en eksisterende database.
/// Tilføjer også nye kolonner til eksisterende tabeller – men kun kolonner der må være tomme (NULL),
/// så eksisterende rækker ikke skal have en værdi. Andre ændringer kræver en manuel opdatering.
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

        var missing = new List<string>();
        foreach (var table in db.Model.GetEntityTypes().Select(e => e.GetTableName()).OfType<string>().Distinct())
        {
            if (!await TableExistsAsync(db, sql.DelimitIdentifier(table))) missing.Add(table);
        }
        if (missing.Count == 0) return;

        // Udfør sætningerne i scriptets rækkefølge, så en tabel oprettes før de tabeller, der peger på den.
        var quoted = missing.Select(t => sql.DelimitIdentifier(t)).ToList();
        var created = new HashSet<string>();
        foreach (var statement in statements)
        {
            var table = quoted.FirstOrDefault(q =>
                statement.StartsWith($"CREATE TABLE {q}", StringComparison.Ordinal)
                || (statement.StartsWith("CREATE ", StringComparison.Ordinal) && statement.Contains($" ON {q}", StringComparison.Ordinal)));
            if (table is null) continue;
            await db.Database.ExecuteSqlRawAsync(statement);
            created.Add(table);
        }

        foreach (var table in missing)
        {
            if (created.Contains(sql.DelimitIdentifier(table))) log.LogInformation("Oprettede manglende tabel {Table}.", table);
            else log.LogError("Tabellen {Table} mangler, men der blev ikke fundet en CREATE-sætning til den.", table);
        }
    }

    public static async Task AddMissingColumnsAsync(AppDbContext db, ILogger log)
    {
        var sql = db.GetService<ISqlGenerationHelper>();
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null) continue;
            var quotedTable = sql.DelimitIdentifier(table);
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());

            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(store);
                if (column is null) continue;
                var quotedColumn = sql.DelimitIdentifier(column);
                if (await ColumnExistsAsync(db, quotedTable, quotedColumn)) continue;

                if (!property.IsNullable)
                {
                    log.LogError("Kolonnen {Table}.{Column} mangler og kan ikke tilføjes automatisk (den må ikke være tom).", table, column);
                    continue;
                }
                // Tabel, kolonne og type kommer fra EF-modellen (ikke fra brugere) og er citeret af databaseudbyderen.
#pragma warning disable EF1002
                await db.Database.ExecuteSqlRawAsync($"ALTER TABLE {quotedTable} ADD {quotedColumn} {property.GetColumnType()} NULL");
#pragma warning restore EF1002
                log.LogInformation("Tilføjede manglende kolonne {Table}.{Column}.", table, column);
            }
        }
    }

    private static Task<bool> ColumnExistsAsync(AppDbContext db, string quotedTable, string quotedColumn) =>
        ProbeAsync(db, $"SELECT {quotedColumn} FROM {quotedTable} WHERE 1 = 0");

    /// <summary>Tjekker direkte på forbindelsen (uden om EF), så en manglende tabel ikke logges som en fejl.</summary>
    private static Task<bool> TableExistsAsync(AppDbContext db, string quotedTable) =>
        ProbeAsync(db, $"SELECT 1 FROM {quotedTable} WHERE 1 = 0");

    private static async Task<bool> ProbeAsync(AppDbContext db, string probe)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = probe;
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
