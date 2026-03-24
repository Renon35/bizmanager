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
                    CONSTRAINT "FK_Products_Catalogs_CatalogId"
                        FOREIGN KEY ("CatalogId") REFERENCES "Catalogs" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_Products_Collections_CollectionId"
                        FOREIGN KEY ("CollectionId") REFERENCES "Collections" ("Id") ON DELETE SET NULL
                );
                """);

            // Incremental columns (won't crash if they already exist)
            AddColumnIfNotExists(conn, "Products", "\"ImageUrl\"",      "TEXT NULL");
            AddColumnIfNotExists(conn, "Products", "\"PurchasePrice\"", "NUMERIC NOT NULL DEFAULT 0");
            AddColumnIfNotExists(conn, "Products", "\"SalePrice\"",     "NUMERIC NOT NULL DEFAULT 0");
            AddColumnIfNotExists(conn, "Products", "\"ListPrice\"",     "NUMERIC NOT NULL DEFAULT 0");
            AddColumnIfNotExists(conn, "Products", "\"MoldCode\"",      "TEXT NULL");
            AddColumnIfNotExists(conn, "Products", "\"Barcode\"",       "TEXT NULL");
            AddColumnIfNotExists(conn, "Products", "\"CollectionId\"",  "INTEGER NULL REFERENCES \"Collections\"(\"Id\") ON DELETE SET NULL");
            AddColumnIfNotExists(conn, "Products", "\"HasMissingImage\"", "BOOLEAN NOT NULL DEFAULT FALSE");

            // ── DealerProducts ───────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "DealerProducts" (
                    "Id"            SERIAL  NOT NULL CONSTRAINT "PK_DealerProducts" PRIMARY KEY,
                    "DealerId"      INTEGER NOT NULL,
                    "ProductId"     INTEGER NOT NULL,
                    "StockQuantity" INTEGER NOT NULL DEFAULT 0,
                    "UnitPrice"     NUMERIC NOT NULL DEFAULT 0,
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
                    "Id"              SERIAL NOT NULL CONSTRAINT "PK_Quotations" PRIMARY KEY,
                    "QuotationNumber" TEXT   NOT NULL DEFAULT '',
                    "DealerId"        INTEGER NULL,
                    "CreatedAt"       TEXT   NOT NULL DEFAULT now()::TEXT,
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
        // PostgreSQL 9.6+ supports IF NOT EXISTS for ADD COLUMN
        try
        {
            Execute(conn, $"ALTER TABLE {table} ADD COLUMN IF NOT EXISTS {column} {definition};");
        }
        catch
        {
            // Swallow: likely already exists on older Postgres
        }
    }

    private static void Execute(NpgsqlConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}


namespace BizManager.Services;

public static class DbMigrator
{
    public static void ApplyMigrations(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        var conn = (SqliteConnection)db.Database.GetDbConnection();
        conn.Open();

        // ----------------------------------------------------------
        // Catalogs table: Create
        // ----------------------------------------------------------
        Execute(conn, """
            CREATE TABLE IF NOT EXISTS "Catalogs" (
                "Id"          INTEGER NOT NULL CONSTRAINT "PK_Catalogs" PRIMARY KEY AUTOINCREMENT,
                "BrandId"     INTEGER NOT NULL,
                "CatalogName" TEXT    NOT NULL,
                "Description" TEXT    NULL,
                "CreatedAt"   TEXT    NOT NULL,
                CONSTRAINT "FK_Catalogs_Brands_BrandId" FOREIGN KEY ("BrandId") REFERENCES "Brands" ("Id") ON DELETE CASCADE
            );
            """);

        // ----------------------------------------------------------
        // Products table: Migrate to new schema (removes BrandId, adds CatalogId, renames fields)
        // ----------------------------------------------------------
        var productColumns = GetColumns(conn, "Products");
        bool needsProductMigration = !productColumns.Contains("CatalogId") 
                                  || !productColumns.Contains("ProductName")
                                  || !productColumns.Contains("ProductCode")
                                  || !productColumns.Contains("PackageType")
                                  || !productColumns.Contains("UnitsPerCase")
                                  || !productColumns.Contains("UnitsPerPack");

        if (needsProductMigration)
        {
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "Products_new" (
                    "Id"           INTEGER NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY AUTOINCREMENT,
                    "CatalogId"    INTEGER NULL,
                    "ProductName"  TEXT    NOT NULL,
                    "ProductCode"  TEXT    NULL,
                    "PackageType"  TEXT    NULL,
                    "UnitsPerCase" INTEGER NULL,
                    "UnitsPerPack" INTEGER NULL,
                    CONSTRAINT "FK_Products_Catalogs_CatalogId" FOREIGN KEY ("CatalogId") REFERENCES "Catalogs" ("Id") ON DELETE SET NULL
                );
                """);

            string nameCol     = productColumns.Contains("Name") ? "\"Name\"" : "''";
            string codeCol     = productColumns.Contains("Code") ? "\"Code\"" : "NULL";
            string typeCol     = productColumns.Contains("Category") ? "\"Category\"" : "NULL";
            string caseSizeCol = productColumns.Contains("CaseSize") ? "\"CaseSize\"" : "NULL";
            string packSizeCol = productColumns.Contains("PackSize") ? "\"PackSize\"" : "NULL";
            string catalogCol  = productColumns.Contains("CatalogId") ? "\"CatalogId\"" : "NULL";

            Execute(conn, $"""
                INSERT INTO "Products_new" ("Id","CatalogId","ProductName","ProductCode","PackageType","UnitsPerCase","UnitsPerPack")
                SELECT "Id", {catalogCol}, {nameCol}, {codeCol}, {typeCol}, {caseSizeCol}, {packSizeCol}
                FROM "Products";
                """);

            Execute(conn, "DROP TABLE \"Products\";");
            Execute(conn, "ALTER TABLE \"Products_new\" RENAME TO \"Products\";");
        }

        // ----------------------------------------------------------
        // DealerProducts table: migrate to new pricing schema
        // ----------------------------------------------------------
        var dpColumns = GetColumns(conn, "DealerProducts");
        bool needsMigration = !dpColumns.Contains("StockQuantity")
                           || !dpColumns.Contains("CasePrice")
                           || !dpColumns.Contains("PackPrice")
                           || !dpColumns.Contains("UnitPrice");

        if (needsMigration)
        {
            // SQLite doesn't support DROP/ALTER COLUMN, so we recreate the table.
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "DealerProducts_new" (
                    "Id"            INTEGER NOT NULL CONSTRAINT "PK_DealerProducts" PRIMARY KEY AUTOINCREMENT,
                    "DealerId"      INTEGER NOT NULL,
                    "ProductId"     INTEGER NOT NULL,
                    "StockQuantity" INTEGER NOT NULL DEFAULT 0,
                    "UnitPrice"     TEXT    NOT NULL DEFAULT '0',
                    "LastUpdated"   TEXT    NOT NULL DEFAULT '0001-01-01 00:00:00',
                    CONSTRAINT "FK_DealerProducts_Dealers_DealerId"
                        FOREIGN KEY ("DealerId") REFERENCES "Dealers" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_DealerProducts_Products_ProductId"
                        FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
                );
                """);

            // Copy existing data, mapping old Stock→StockQuantity, old Price→UnitPrice
            bool hasOldStock = dpColumns.Contains("Stock");
            bool hasOldPrice = dpColumns.Contains("Price");
            bool hasOldUpdatedAt = dpColumns.Contains("UpdatedAt");

            string stockSrc = hasOldStock ? "\"Stock\"" : "0";
            string priceSrc = hasOldPrice ? "\"Price\"" : "'0'";
            string dateSrc = hasOldUpdatedAt ? "\"UpdatedAt\"" : "'0001-01-01 00:00:00'";

            Execute(conn, $"""
                INSERT INTO "DealerProducts_new"
                    ("Id","DealerId","ProductId","StockQuantity","UnitPrice","LastUpdated")
                SELECT
                    "Id","DealerId","ProductId",{stockSrc},
                    COALESCE({priceSrc},'0'),
                    COALESCE({dateSrc},'0001-01-01 00:00:00')
                FROM "DealerProducts";
                """);

            Execute(conn, "DROP TABLE \"DealerProducts\";");
            Execute(conn, "ALTER TABLE \"DealerProducts_new\" RENAME TO \"DealerProducts\";");
        }

        // ----------------------------------------------------------
        // BrandCatalogs table: ensure it exists
        // ----------------------------------------------------------
        Execute(conn, """
            CREATE TABLE IF NOT EXISTS "BrandCatalogs" (
                "Id"               INTEGER NOT NULL CONSTRAINT "PK_BrandCatalogs" PRIMARY KEY AUTOINCREMENT,
                "BrandId"          INTEGER NOT NULL,
                "OriginalFileName" TEXT    NOT NULL,
                "CustomFileName"   TEXT    NOT NULL,
                "FilePath"         TEXT    NOT NULL,
                "UploadedAt"       TEXT    NOT NULL,
                CONSTRAINT "FK_BrandCatalogs_Brands_BrandId" FOREIGN KEY ("BrandId") REFERENCES "Brands" ("Id") ON DELETE CASCADE
            );
            """);

        // ----------------------------------------------------------
        // Quotations table: add DealerId and VAT fields
        // ----------------------------------------------------------
        var qColumns = GetColumns(conn, "Quotations");
        if (!qColumns.Contains("DealerId"))    Execute(conn, "ALTER TABLE \"Quotations\" ADD COLUMN \"DealerId\" INTEGER NULL REFERENCES \"Dealers\"(\"Id\") ON DELETE SET NULL;");
        if (!qColumns.Contains("Subtotal"))    Execute(conn, "ALTER TABLE \"Quotations\" ADD COLUMN \"Subtotal\" TEXT NOT NULL DEFAULT '0';");
        if (!qColumns.Contains("VatRate"))     Execute(conn, "ALTER TABLE \"Quotations\" ADD COLUMN \"VatRate\" TEXT NOT NULL DEFAULT '20.0';");
        if (!qColumns.Contains("VatAmount"))   Execute(conn, "ALTER TABLE \"Quotations\" ADD COLUMN \"VatAmount\" TEXT NOT NULL DEFAULT '0';");
        if (!qColumns.Contains("GrandTotal"))  
        {
            Execute(conn, "ALTER TABLE \"Quotations\" ADD COLUMN \"GrandTotal\" TEXT NOT NULL DEFAULT '0';");
            // Map existing TotalPrice to Subtotal and GrandTotal
            Execute(conn, "UPDATE \"Quotations\" SET \"Subtotal\" = \"TotalPrice\", \"GrandTotal\" = \"TotalPrice\" WHERE \"GrandTotal\" = '0';");
        }

        // ----------------------------------------------------------
        // ImageUrl Columns: add to Products and QuotationItems
        // ----------------------------------------------------------
        var pCols = GetColumns(conn, "Products");
        if (!pCols.Contains("ImageUrl")) Execute(conn, "ALTER TABLE \"Products\" ADD COLUMN \"ImageUrl\" TEXT NULL;");
        if (!pCols.Contains("PurchasePrice")) Execute(conn, "ALTER TABLE \"Products\" ADD COLUMN \"PurchasePrice\" TEXT NOT NULL DEFAULT '0';");
        if (!pCols.Contains("SalePrice")) Execute(conn, "ALTER TABLE \"Products\" ADD COLUMN \"SalePrice\" TEXT NOT NULL DEFAULT '0';");
        if (!pCols.Contains("ListPrice")) Execute(conn, "ALTER TABLE \"Products\" ADD COLUMN \"ListPrice\" TEXT NOT NULL DEFAULT '0';");
        if (!pCols.Contains("MoldCode")) Execute(conn, "ALTER TABLE \"Products\" ADD COLUMN \"MoldCode\" TEXT NULL;");
        if (!pCols.Contains("Barcode")) Execute(conn, "ALTER TABLE \"Products\" ADD COLUMN \"Barcode\" TEXT NULL;");
        
        // Remove IX_Products_ProductCode index if it exists, since product codes are no longer globally unique
        try { Execute(conn, "DROP INDEX IF EXISTS \"IX_Products_ProductCode\";"); } catch { }
        
        var qiCols = GetColumns(conn, "QuotationItems");
        if (!qiCols.Contains("ImageUrl"))
        {
            Execute(conn, "ALTER TABLE \"QuotationItems\" ADD COLUMN \"ImageUrl\" TEXT NULL;");
        }

        var brandCols = GetColumns(conn, "Brands");
        if (!brandCols.Contains("CodeStructure")) Execute(conn, "ALTER TABLE \"Brands\" ADD COLUMN \"CodeStructure\" TEXT NOT NULL DEFAULT 'single_code';");

        // ----------------------------------------------------------
        // Sales Orders, Shipments & Delivery Items
        // ----------------------------------------------------------
        Execute(conn, """
            CREATE TABLE IF NOT EXISTS "SalesOrders" (
                "Id"          INTEGER NOT NULL CONSTRAINT "PK_SalesOrders" PRIMARY KEY AUTOINCREMENT,
                "OrderNumber" TEXT    NOT NULL,
                "CustomerId"  INTEGER NOT NULL,
                "SalesRepId"  INTEGER NOT NULL,
                "OrderDate"   TEXT    NOT NULL,
                "Status"      TEXT    NOT NULL,
                CONSTRAINT "FK_SalesOrders_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_SalesOrders_SalesReps_SalesRepId" FOREIGN KEY ("SalesRepId") REFERENCES "SalesReps" ("Id") ON DELETE CASCADE
            );
            """);

        Execute(conn, """
            CREATE TABLE IF NOT EXISTS "SalesOrderItems" (
                "Id"           INTEGER NOT NULL CONSTRAINT "PK_SalesOrderItems" PRIMARY KEY AUTOINCREMENT,
                "SalesOrderId" INTEGER NOT NULL,
                "ProductId"    INTEGER NOT NULL,
                "Quantity"     INTEGER NOT NULL,
                "UnitPrice"    TEXT    NOT NULL,
                "TotalPrice"   TEXT    NOT NULL,
                CONSTRAINT "FK_SalesOrderItems_SalesOrders_SalesOrderId" FOREIGN KEY ("SalesOrderId") REFERENCES "SalesOrders" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_SalesOrderItems_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
            );
            """);

        Execute(conn, """
            CREATE TABLE IF NOT EXISTS "SalesShipments" (
                "Id"              INTEGER NOT NULL CONSTRAINT "PK_SalesShipments" PRIMARY KEY AUTOINCREMENT,
                "SalesOrderId"    INTEGER NOT NULL,
                "ShipmentDate"    TEXT    NOT NULL,
                "Status"          TEXT    NOT NULL,
                "ShippingCompany" TEXT    NULL,
                "TrackingNumber"  TEXT    NULL,
                CONSTRAINT "FK_SalesShipments_SalesOrders_SalesOrderId" FOREIGN KEY ("SalesOrderId") REFERENCES "SalesOrders" ("Id") ON DELETE CASCADE
            );
            """);

        Execute(conn, """
            CREATE TABLE IF NOT EXISTS "DeliveryItems" (
                "Id"                   INTEGER NOT NULL CONSTRAINT "PK_DeliveryItems" PRIMARY KEY AUTOINCREMENT,
                "SalesShipmentId"      INTEGER NOT NULL,
                "ProductId"            INTEGER NOT NULL,
                "OrderedQuantity"      INTEGER NOT NULL,
                "DeliveredQuantity"    INTEGER NOT NULL,
                "MissingQuantity"      INTEGER NOT NULL,
                "ExpectedDeliveryDate" TEXT    NULL,
                "Note"                 TEXT    NULL,
                "Status"               TEXT    NOT NULL,
                CONSTRAINT "FK_DeliveryItems_SalesShipments_SalesShipmentId" FOREIGN KEY ("SalesShipmentId") REFERENCES "SalesShipments" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_DeliveryItems_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
            );
            """);

        // ----------------------------------------------------------
        // Collections table: ensure it exists
        // ----------------------------------------------------------
        Execute(conn, """
            CREATE TABLE IF NOT EXISTS "Collections" (
                "Id"             INTEGER NOT NULL CONSTRAINT "PK_Collections" PRIMARY KEY AUTOINCREMENT,
                "CatalogId"      INTEGER NOT NULL,
                "CollectionName" TEXT    NOT NULL,
                "Description"    TEXT    NULL,
                CONSTRAINT "FK_Collections_Catalogs_CatalogId" FOREIGN KEY ("CatalogId") REFERENCES "Catalogs" ("Id") ON DELETE CASCADE
            );
            """);

        // ----------------------------------------------------------
        // Products table checks for CollectionId
        // ----------------------------------------------------------
        var prodCols = GetColumns(conn, "Products");
        if (!prodCols.Contains("CollectionId"))
        {
            Execute(conn, "ALTER TABLE \"Products\" ADD COLUMN \"CollectionId\" INTEGER NULL REFERENCES \"Collections\"(\"Id\") ON DELETE SET NULL;");
        }

        conn.Close();
    }

    /// <summary>Returns true if the Products.BrandId column is NOT NULL in the current schema.</summary>
    private static bool IsBrandIdNotNull(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(\"Products\");";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string colName = reader.GetString(1); // name
            if (colName.Equals("BrandId", StringComparison.OrdinalIgnoreCase))
            {
                int notNullFlag = reader.GetInt32(3); // notnull: 1 = NOT NULL
                return notNullFlag == 1;
            }
        }
        return false; // column not found => assume ok
    }

    private static HashSet<string> GetColumns(SqliteConnection conn, string table)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\");";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            set.Add(reader.GetString(1)); // column 1 = name
        return set;
    }

    private static void Execute(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
