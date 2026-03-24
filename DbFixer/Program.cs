using Microsoft.Data.Sqlite;
using System;
using System.IO;

var dbPath = Path.GetFullPath(@"..\BizManager\bizmanager.db");
Console.WriteLine($"Fixing DB at {dbPath}");

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

void PassCommand(string sql)
{
    try 
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        int rows = command.ExecuteNonQuery();
        Console.WriteLine($"OK: {sql} ({rows} rows)");
    } 
    catch (Exception ex)
    {
        Console.WriteLine($"Skipping/Error: {sql} -> {ex.Message}");
    }
}

PassCommand("ALTER TABLE Brands ADD COLUMN WebsiteDomain TEXT NULL;");
PassCommand("ALTER TABLE Products ADD COLUMN HasMissingImage INTEGER NOT NULL DEFAULT 0;");
PassCommand("INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20260309110855_AddQuotationItemImage', '10.0.3');");
PassCommand("INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20260310184253_AddProductImageScrapingFields', '10.0.3');");

Console.WriteLine("Done.");
