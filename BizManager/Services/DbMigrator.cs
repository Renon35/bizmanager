using BizManager.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BizManager.Services;

public static class DbMigrator
{
    public static void ApplyMigrations(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Let EF Core create the schema from the model (safe on empty DB, no-op on existing schema)
        db.Database.EnsureCreated();

        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        conn.Open();

        try
        {
            // ── Brands ────────────────────────────────────────────────────────
            AddColumnIfNotExists(conn, "Brands", "\"CodeStructure\"", "TEXT NOT NULL DEFAULT 'single_code'");
            AddColumnIfNotExists(conn, "Brands", "\"WebsiteDomain\"",  "TEXT NULL");
            AddColumnIfNotExists(conn, "Brands", "\"LogoPath\"",       "TEXT NULL");

            // ── Catalogs ─────────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "Catalogs" (
                    "Id"          SERIAL  NOT NULL CONSTRAINT "PK_Catalogs" PRIMARY KEY,
                    "BrandId"     INTEGER NOT NULL,
                    "CatalogName" TEXT    NOT NULL,
                    "Description" TEXT    NULL,
                    "CreatedAt"   TEXT    NOT NULL DEFAULT now()::TEXT,
                    CONSTRAINT "FK_Catalogs_Brands_BrandId"
                        FOREIGN KEY ("BrandId") REFERENCES "Brands" ("Id") ON DELETE CASCADE
                );
                """);

            // ── Collections ──────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "Collections" (
                    "Id"             SERIAL  NOT NULL CONSTRAINT "PK_Collections" PRIMARY KEY,
                    "CatalogId"      INTEGER NOT NULL,
                    "CollectionName" TEXT    NOT NULL,
                    "Description"    TEXT    NULL,
                    CONSTRAINT "FK_Collections_Catalogs_CatalogId"
                        FOREIGN KEY ("CatalogId") REFERENCES "Catalogs" ("Id") ON DELETE CASCADE
                );
                """);

            // ── Products ─────────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "Products" (
                    "Id"            SERIAL  NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY,
                    "CatalogId"     INTEGER NULL,
                    "CollectionId"  INTEGER NULL,
                    "ProductName"   TEXT    NOT NULL DEFAULT '',
                    "ProductCode"   TEXT    NULL,
                    "MoldCode"      TEXT    NULL,
                    "Barcode"       TEXT    NULL,
                    "PackageType"   TEXT    NULL,
                    "UnitsPerCase"  INTEGER NULL,
                    "UnitsPerPack"  INTEGER NULL,
                    "PurchasePrice" NUMERIC NOT NULL DEFAULT 0,
                    "SalePrice"     NUMERIC NOT NULL DEFAULT 0,
                    "ListPrice"     NUMERIC NOT NULL DEFAULT 0,
                    "ImageUrl"      TEXT    NULL,
                    "HasMissingImage" BOOLEAN NOT NULL DEFAULT FALSE,
                    CONSTRAINT "FK_Products_Catalogs_CatalogId"
                        FOREIGN KEY ("CatalogId") REFERENCES "Catalogs" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_Products_Collections_CollectionId"
                        FOREIGN KEY ("CollectionId") REFERENCES "Collections" ("Id") ON DELETE SET NULL
                );
                """);

            AddColumnIfNotExists(conn, "Products", "\"ImageUrl\"",        "TEXT NULL");
            AddColumnIfNotExists(conn, "Products", "\"PurchasePrice\"",   "NUMERIC NOT NULL DEFAULT 0");
            AddColumnIfNotExists(conn, "Products", "\"SalePrice\"",       "NUMERIC NOT NULL DEFAULT 0");
            AddColumnIfNotExists(conn, "Products", "\"ListPrice\"",       "NUMERIC NOT NULL DEFAULT 0");
            AddColumnIfNotExists(conn, "Products", "\"MoldCode\"",        "TEXT NULL");
            AddColumnIfNotExists(conn, "Products", "\"Barcode\"",         "TEXT NULL");
            AddColumnIfNotExists(conn, "Products", "\"CollectionId\"",    "INTEGER NULL REFERENCES \"Collections\"(\"Id\") ON DELETE SET NULL");
            AddColumnIfNotExists(conn, "Products", "\"HasMissingImage\"", "BOOLEAN NOT NULL DEFAULT FALSE");

            // ── DealerProducts ───────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "DealerProducts" (
                    "Id"            SERIAL    NOT NULL CONSTRAINT "PK_DealerProducts" PRIMARY KEY,
                    "DealerId"      INTEGER   NOT NULL,
                    "ProductId"     INTEGER   NOT NULL,
                    "StockQuantity" INTEGER   NOT NULL DEFAULT 0,
                    "UnitPrice"     NUMERIC   NOT NULL DEFAULT 0,
                    "LastUpdated"   TIMESTAMP NOT NULL DEFAULT now(),
                    CONSTRAINT "FK_DealerProducts_Dealers_DealerId"
                        FOREIGN KEY ("DealerId") REFERENCES "Dealers" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_DealerProducts_Products_ProductId"
                        FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
                );
                """);

            AddColumnIfNotExists(conn, "DealerProducts", "\"StockQuantity\"", "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfNotExists(conn, "DealerProducts", "\"UnitPrice\"",     "NUMERIC NOT NULL DEFAULT 0");

            // ── BrandCatalogs ────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "BrandCatalogs" (
                    "Id"               SERIAL NOT NULL CONSTRAINT "PK_BrandCatalogs" PRIMARY KEY,
                    "BrandId"          INTEGER NOT NULL,
                    "OriginalFileName" TEXT    NOT NULL DEFAULT '',
                    "CustomFileName"   TEXT    NOT NULL DEFAULT '',
                    "FilePath"         TEXT    NOT NULL DEFAULT '',
                    "UploadedAt"       TEXT    NOT NULL DEFAULT now()::TEXT,
                    CONSTRAINT "FK_BrandCatalogs_Brands_BrandId"
                        FOREIGN KEY ("BrandId") REFERENCES "Brands" ("Id") ON DELETE CASCADE
                );
                """);

            // ── Quotations ───────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "Quotations" (
                    "Id"              SERIAL  NOT NULL CONSTRAINT "PK_Quotations" PRIMARY KEY,
                    "QuotationNumber" TEXT    NOT NULL DEFAULT '',
                    "DealerId"        INTEGER NULL,
                    "CreatedAt"       TEXT    NOT NULL DEFAULT now()::TEXT,
                    "TotalPrice"      NUMERIC NOT NULL DEFAULT 0,
                    "Subtotal"        NUMERIC NOT NULL DEFAULT 0,
                    "VatRate"         NUMERIC NOT NULL DEFAULT 20.0,
                    "VatAmount"       NUMERIC NOT NULL DEFAULT 0,
                    "GrandTotal"      NUMERIC NOT NULL DEFAULT 0,
                    CONSTRAINT "FK_Quotations_Dealers_DealerId"
                        FOREIGN KEY ("DealerId") REFERENCES "Dealers" ("Id") ON DELETE SET NULL
                );
                """);

            AddColumnIfNotExists(conn, "Quotations", "\"DealerId\"",   "INTEGER NULL REFERENCES \"Dealers\"(\"Id\") ON DELETE SET NULL");
            AddColumnIfNotExists(conn, "Quotations", "\"Subtotal\"",   "NUMERIC NOT NULL DEFAULT 0");
            AddColumnIfNotExists(conn, "Quotations", "\"VatRate\"",    "NUMERIC NOT NULL DEFAULT 20.0");
            AddColumnIfNotExists(conn, "Quotations", "\"VatAmount\"",  "NUMERIC NOT NULL DEFAULT 0");
            AddColumnIfNotExists(conn, "Quotations", "\"GrandTotal\"", "NUMERIC NOT NULL DEFAULT 0");

            // ── QuotationItems ───────────────────────────────────────────────
            AddColumnIfNotExists(conn, "QuotationItems", "\"ImageUrl\"", "TEXT NULL");

            // ── SalesOrders ──────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "SalesOrders" (
                    "Id"          SERIAL  NOT NULL CONSTRAINT "PK_SalesOrders" PRIMARY KEY,
                    "OrderNumber" TEXT    NOT NULL DEFAULT '',
                    "CustomerId"  INTEGER NOT NULL,
                    "SalesRepId"  INTEGER NOT NULL,
                    "OrderDate"   TEXT    NOT NULL DEFAULT now()::TEXT,
                    "Status"      TEXT    NOT NULL DEFAULT 'pending',
                    "QuotationId" INTEGER NULL,
                    CONSTRAINT "FK_SalesOrders_Customers_CustomerId"
                        FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_SalesOrders_SalesReps_SalesRepId"
                        FOREIGN KEY ("SalesRepId") REFERENCES "SalesReps" ("Id") ON DELETE CASCADE
                );
                """);

            AddColumnIfNotExists(conn, "SalesOrders", "\"QuotationId\"", "INTEGER NULL");

            // ── SalesOrderItems ──────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "SalesOrderItems" (
                    "Id"           SERIAL  NOT NULL CONSTRAINT "PK_SalesOrderItems" PRIMARY KEY,
                    "SalesOrderId" INTEGER NOT NULL,
                    "ProductId"    INTEGER NOT NULL,
                    "Quantity"     INTEGER NOT NULL DEFAULT 1,
                    "UnitPrice"    NUMERIC NOT NULL DEFAULT 0,
                    "TotalPrice"   NUMERIC NOT NULL DEFAULT 0,
                    CONSTRAINT "FK_SalesOrderItems_SalesOrders_SalesOrderId"
                        FOREIGN KEY ("SalesOrderId") REFERENCES "SalesOrders" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_SalesOrderItems_Products_ProductId"
                        FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
                );
                """);

            // ── SalesShipments ───────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "SalesShipments" (
                    "Id"              SERIAL NOT NULL CONSTRAINT "PK_SalesShipments" PRIMARY KEY,
                    "SalesOrderId"    INTEGER NOT NULL,
                    "ShipmentDate"    TEXT    NOT NULL DEFAULT now()::TEXT,
                    "Status"          TEXT    NOT NULL DEFAULT 'pending',
                    "ShippingCompany" TEXT    NULL,
                    "TrackingNumber"  TEXT    NULL,
                    CONSTRAINT "FK_SalesShipments_SalesOrders_SalesOrderId"
                        FOREIGN KEY ("SalesOrderId") REFERENCES "SalesOrders" ("Id") ON DELETE CASCADE
                );
                """);

            // ── DeliveryItems ────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "DeliveryItems" (
                    "Id"                   SERIAL NOT NULL CONSTRAINT "PK_DeliveryItems" PRIMARY KEY,
                    "SalesShipmentId"      INTEGER NOT NULL,
                    "ProductId"            INTEGER NOT NULL,
                    "OrderedQuantity"      INTEGER NOT NULL DEFAULT 0,
                    "DeliveredQuantity"    INTEGER NOT NULL DEFAULT 0,
                    "MissingQuantity"      INTEGER NOT NULL DEFAULT 0,
                    "ExpectedDeliveryDate" TEXT    NULL,
                    "Note"                 TEXT    NULL,
                    "Status"               TEXT    NOT NULL DEFAULT 'pending',
                    CONSTRAINT "FK_DeliveryItems_SalesShipments_SalesShipmentId"
                        FOREIGN KEY ("SalesShipmentId") REFERENCES "SalesShipments" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_DeliveryItems_Products_ProductId"
                        FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
                );
                """);

            // ── ProductImportPreviews ────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "ProductImportPreviews" (
                    "Id"          SERIAL  NOT NULL CONSTRAINT "PK_ProductImportPreviews" PRIMARY KEY,
                    "BrandId"     INTEGER NOT NULL DEFAULT 0,
                    "CatalogId"   INTEGER NULL,
                    "DealerId"    INTEGER NOT NULL DEFAULT 0,
                    "ProductCode" TEXT    NOT NULL DEFAULT '',
                    "MoldCode"    TEXT    NOT NULL DEFAULT '',
                    "Barcode"     TEXT    NOT NULL DEFAULT '',
                    "ProductName" TEXT    NOT NULL DEFAULT '',
                    "Collection"  TEXT    NOT NULL DEFAULT '',
                    "Price"       NUMERIC NOT NULL DEFAULT 0,
                    "Stock"       INTEGER NOT NULL DEFAULT 0,
                    "IsHeader"    BOOLEAN NOT NULL DEFAULT FALSE,
                    "Status"      TEXT    NOT NULL DEFAULT 'Ready',
                    "PriceType"   TEXT    NOT NULL DEFAULT 'purchase_price'
                );
                """);
        }
        finally
        {
            conn.Close();
        }
    }

    private static void AddColumnIfNotExists(NpgsqlConnection conn, string table, string column, string definition)
    {
        try
        {
            Execute(conn, $"ALTER TABLE {table} ADD COLUMN IF NOT EXISTS {column} {definition};");
        }
        catch
        {
            // Swallow: column already exists on older Postgres
        }
    }

    private static void Execute(NpgsqlConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
