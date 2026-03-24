using BizManager.Data;
using Npgsql;

namespace BizManager.Services;

public static class DbMigrator
{
    public static void ApplyMigrations(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Use raw Npgsql connection — do NOT call EnsureCreated() on pooler connections.
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        conn.Open();

        try
        {
            // ── Brands ────────────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "Brands" (
                    "Id"            SERIAL  NOT NULL PRIMARY KEY,
                    "Name"          TEXT    NOT NULL DEFAULT '',
                    "Description"   TEXT    NULL,
                    "LogoPath"      TEXT    NULL,
                    "CreatedAt"     TIMESTAMP NOT NULL DEFAULT now(),
                    "CodeStructure" TEXT    NOT NULL DEFAULT 'single_code',
                    "WebsiteDomain" TEXT    NULL
                );
                """);

            // ── Customers ─────────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "Customers" (
                    "Id"             SERIAL NOT NULL PRIMARY KEY,
                    "CompanyName"    TEXT   NOT NULL DEFAULT '',
                    "Representative" TEXT   NULL,
                    "Phone"          TEXT   NULL,
                    "Email"          TEXT   NULL,
                    "Address"        TEXT   NULL,
                    "TaxNumber"      TEXT   NULL
                );
                """);

            // ── SalesReps ─────────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "SalesReps" (
                    "Id"        SERIAL NOT NULL PRIMARY KEY,
                    "FirstName" TEXT   NOT NULL DEFAULT '',
                    "LastName"  TEXT   NOT NULL DEFAULT '',
                    "Phone"     TEXT   NULL,
                    "Email"     TEXT   NULL,
                    "LogoPath"  TEXT   NULL
                );
                """);

            // ── Dealers ───────────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "Dealers" (
                    "Id"            SERIAL  NOT NULL PRIMARY KEY,
                    "BrandId"       INTEGER NOT NULL,
                    "Name"          TEXT    NOT NULL DEFAULT '',
                    "ContactPerson" TEXT    NULL,
                    "Phone"         TEXT    NULL,
                    "Email"         TEXT    NULL,
                    "Address"       TEXT    NULL,
                    "Notes"         TEXT    NULL,
                    CONSTRAINT "FK_Dealers_Brands_BrandId"
                        FOREIGN KEY ("BrandId") REFERENCES "Brands"("Id") ON DELETE CASCADE
                );
                """);

            // ── Catalogs ──────────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "Catalogs" (
                    "Id"          SERIAL  NOT NULL PRIMARY KEY,
                    "BrandId"     INTEGER NOT NULL,
                    "CatalogName" TEXT    NOT NULL DEFAULT '',
                    "Description" TEXT    NULL,
                    "CreatedAt"   TEXT    NOT NULL DEFAULT now()::TEXT,
                    CONSTRAINT "FK_Catalogs_Brands_BrandId"
                        FOREIGN KEY ("BrandId") REFERENCES "Brands"("Id") ON DELETE CASCADE
                );
                """);

            // ── Collections ───────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "Collections" (
                    "Id"             SERIAL  NOT NULL PRIMARY KEY,
                    "CatalogId"      INTEGER NOT NULL,
                    "CollectionName" TEXT    NOT NULL DEFAULT '',
                    "Description"    TEXT    NULL,
                    CONSTRAINT "FK_Collections_Catalogs_CatalogId"
                        FOREIGN KEY ("CatalogId") REFERENCES "Catalogs"("Id") ON DELETE CASCADE
                );
                """);

            // ── Products ──────────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "Products" (
                    "Id"              SERIAL   NOT NULL PRIMARY KEY,
                    "CatalogId"       INTEGER  NULL,
                    "CollectionId"    INTEGER  NULL,
                    "ProductName"     TEXT     NOT NULL DEFAULT '',
                    "ProductCode"     TEXT     NULL,
                    "MoldCode"        TEXT     NULL,
                    "Barcode"         TEXT     NULL,
                    "PackageType"     TEXT     NULL,
                    "UnitsPerCase"    INTEGER  NULL,
                    "UnitsPerPack"    INTEGER  NULL,
                    "PurchasePrice"   NUMERIC  NOT NULL DEFAULT 0,
                    "SalePrice"       NUMERIC  NOT NULL DEFAULT 0,
                    "ListPrice"       NUMERIC  NOT NULL DEFAULT 0,
                    "ImageUrl"        TEXT     NULL,
                    "HasMissingImage" BOOLEAN  NOT NULL DEFAULT FALSE,
                    CONSTRAINT "FK_Products_Catalogs_CatalogId"
                        FOREIGN KEY ("CatalogId") REFERENCES "Catalogs"("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_Products_Collections_CollectionId"
                        FOREIGN KEY ("CollectionId") REFERENCES "Collections"("Id") ON DELETE SET NULL
                );
                """);

            // ── DealerProducts ────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "DealerProducts" (
                    "Id"            SERIAL    NOT NULL PRIMARY KEY,
                    "DealerId"      INTEGER   NOT NULL,
                    "ProductId"     INTEGER   NOT NULL,
                    "StockQuantity" INTEGER   NOT NULL DEFAULT 0,
                    "UnitPrice"     NUMERIC   NOT NULL DEFAULT 0,
                    "LastUpdated"   TIMESTAMP NOT NULL DEFAULT now(),
                    CONSTRAINT "FK_DealerProducts_Dealers_DealerId"
                        FOREIGN KEY ("DealerId") REFERENCES "Dealers"("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_DealerProducts_Products_ProductId"
                        FOREIGN KEY ("ProductId") REFERENCES "Products"("Id") ON DELETE CASCADE
                );
                """);

            // ── PurchaseOrders ────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "PurchaseOrders" (
                    "Id"          SERIAL    NOT NULL PRIMARY KEY,
                    "OrderNumber" TEXT      NOT NULL DEFAULT '',
                    "DealerId"    INTEGER   NOT NULL,
                    "OrderDate"   TIMESTAMP NOT NULL DEFAULT now(),
                    "Status"      TEXT      NOT NULL DEFAULT 'preparing',
                    CONSTRAINT "FK_PurchaseOrders_Dealers_DealerId"
                        FOREIGN KEY ("DealerId") REFERENCES "Dealers"("Id") ON DELETE CASCADE
                );
                """);

            // ── OrderItems ────────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "OrderItems" (
                    "Id"         SERIAL  NOT NULL PRIMARY KEY,
                    "OrderId"    INTEGER NOT NULL,
                    "ProductId"  INTEGER NOT NULL,
                    "Quantity"   INTEGER NOT NULL DEFAULT 0,
                    "UnitPrice"  NUMERIC NOT NULL DEFAULT 0,
                    "TotalPrice" NUMERIC NOT NULL DEFAULT 0,
                    CONSTRAINT "FK_OrderItems_PurchaseOrders_OrderId"
                        FOREIGN KEY ("OrderId") REFERENCES "PurchaseOrders"("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_OrderItems_Products_ProductId"
                        FOREIGN KEY ("ProductId") REFERENCES "Products"("Id") ON DELETE CASCADE
                );
                """);

            // ── Shipments ─────────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "Shipments" (
                    "Id"              SERIAL    NOT NULL PRIMARY KEY,
                    "OrderId"         INTEGER   NOT NULL UNIQUE,
                    "ShippingCompany" TEXT      NULL,
                    "TrackingNumber"  TEXT      NULL,
                    "ShipmentDate"    TIMESTAMP NULL,
                    "DeliveryStatus"  TEXT      NULL,
                    CONSTRAINT "FK_Shipments_PurchaseOrders_OrderId"
                        FOREIGN KEY ("OrderId") REFERENCES "PurchaseOrders"("Id") ON DELETE CASCADE
                );
                """);

            // ── DealerInvoices ────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "DealerInvoices" (
                    "Id"            SERIAL    NOT NULL PRIMARY KEY,
                    "OrderId"       INTEGER   NOT NULL UNIQUE,
                    "Issued"        BOOLEAN   NOT NULL DEFAULT FALSE,
                    "InvoiceNumber" TEXT      NULL,
                    "InvoiceDate"   TIMESTAMP NULL,
                    "FilePath"      TEXT      NULL,
                    CONSTRAINT "FK_DealerInvoices_PurchaseOrders_OrderId"
                        FOREIGN KEY ("OrderId") REFERENCES "PurchaseOrders"("Id") ON DELETE CASCADE
                );
                """);

            // ── Quotations ────────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "Quotations" (
                    "Id"              SERIAL    NOT NULL PRIMARY KEY,
                    "QuotationNumber" TEXT      NOT NULL DEFAULT '',
                    "SalesRepId"      INTEGER   NOT NULL,
                    "CustomerId"      INTEGER   NOT NULL,
                    "DealerId"        INTEGER   NULL,
                    "Date"            TIMESTAMP NOT NULL DEFAULT now(),
                    "TotalPrice"      NUMERIC   NOT NULL DEFAULT 0,
                    "Subtotal"        NUMERIC   NOT NULL DEFAULT 0,
                    "VatRate"         NUMERIC   NOT NULL DEFAULT 20.0,
                    "VatAmount"       NUMERIC   NOT NULL DEFAULT 0,
                    "GrandTotal"      NUMERIC   NOT NULL DEFAULT 0,
                    CONSTRAINT "FK_Quotations_SalesReps_SalesRepId"
                        FOREIGN KEY ("SalesRepId") REFERENCES "SalesReps"("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_Quotations_Customers_CustomerId"
                        FOREIGN KEY ("CustomerId") REFERENCES "Customers"("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_Quotations_Dealers_DealerId"
                        FOREIGN KEY ("DealerId") REFERENCES "Dealers"("Id") ON DELETE SET NULL
                );
                """);

            // ── QuotationItems ────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "QuotationItems" (
                    "Id"          SERIAL  NOT NULL PRIMARY KEY,
                    "QuotationId" INTEGER NOT NULL,
                    "ProductName" TEXT    NOT NULL DEFAULT '',
                    "ProductCode" TEXT    NULL,
                    "Quantity"    INTEGER NOT NULL DEFAULT 0,
                    "UnitPrice"   NUMERIC NOT NULL DEFAULT 0,
                    "TotalPrice"  NUMERIC NOT NULL DEFAULT 0,
                    "ImageUrl"    TEXT    NULL,
                    CONSTRAINT "FK_QuotationItems_Quotations_QuotationId"
                        FOREIGN KEY ("QuotationId") REFERENCES "Quotations"("Id") ON DELETE CASCADE
                );
                """);

            // ── Sales ─────────────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "Sales" (
                    "Id"          SERIAL    NOT NULL PRIMARY KEY,
                    "CustomerId"  INTEGER   NOT NULL,
                    "SalesRepId"  INTEGER   NOT NULL,
                    "SaleDate"    TIMESTAMP NOT NULL DEFAULT now(),
                    "TotalPrice"  NUMERIC   NOT NULL DEFAULT 0,
                    "QuotationId" INTEGER   NULL,
                    CONSTRAINT "FK_Sales_Customers_CustomerId"
                        FOREIGN KEY ("CustomerId") REFERENCES "Customers"("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_Sales_SalesReps_SalesRepId"
                        FOREIGN KEY ("SalesRepId") REFERENCES "SalesReps"("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_Sales_Quotations_QuotationId"
                        FOREIGN KEY ("QuotationId") REFERENCES "Quotations"("Id") ON DELETE SET NULL
                );
                """);

            // ── CustomerInvoices ──────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "CustomerInvoices" (
                    "Id"            SERIAL    NOT NULL PRIMARY KEY,
                    "SaleId"        INTEGER   NOT NULL UNIQUE,
                    "Issued"        BOOLEAN   NOT NULL DEFAULT FALSE,
                    "InvoiceNumber" TEXT      NULL,
                    "InvoiceDate"   TIMESTAMP NULL,
                    "FilePath"      TEXT      NULL,
                    CONSTRAINT "FK_CustomerInvoices_Sales_SaleId"
                        FOREIGN KEY ("SaleId") REFERENCES "Sales"("Id") ON DELETE CASCADE
                );
                """);

            // ── SalesOrders ───────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "SalesOrders" (
                    "Id"          SERIAL    NOT NULL PRIMARY KEY,
                    "OrderNumber" TEXT      NOT NULL DEFAULT '',
                    "CustomerId"  INTEGER   NOT NULL,
                    "SalesRepId"  INTEGER   NOT NULL,
                    "OrderDate"   TEXT      NOT NULL DEFAULT now()::TEXT,
                    "Status"      TEXT      NOT NULL DEFAULT 'pending',
                    "QuotationId" INTEGER   NULL,
                    CONSTRAINT "FK_SalesOrders_Customers_CustomerId"
                        FOREIGN KEY ("CustomerId") REFERENCES "Customers"("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_SalesOrders_SalesReps_SalesRepId"
                        FOREIGN KEY ("SalesRepId") REFERENCES "SalesReps"("Id") ON DELETE CASCADE
                );
                """);

            // ── SalesOrderItems ───────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "SalesOrderItems" (
                    "Id"           SERIAL  NOT NULL PRIMARY KEY,
                    "SalesOrderId" INTEGER NOT NULL,
                    "ProductId"    INTEGER NOT NULL,
                    "Quantity"     INTEGER NOT NULL DEFAULT 1,
                    "UnitPrice"    NUMERIC NOT NULL DEFAULT 0,
                    "TotalPrice"   NUMERIC NOT NULL DEFAULT 0,
                    CONSTRAINT "FK_SalesOrderItems_SalesOrders_SalesOrderId"
                        FOREIGN KEY ("SalesOrderId") REFERENCES "SalesOrders"("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_SalesOrderItems_Products_ProductId"
                        FOREIGN KEY ("ProductId") REFERENCES "Products"("Id") ON DELETE RESTRICT
                );
                """);

            // ── SalesShipments ────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "SalesShipments" (
                    "Id"              SERIAL  NOT NULL PRIMARY KEY,
                    "SalesOrderId"    INTEGER NOT NULL,
                    "ShipmentDate"    TEXT    NOT NULL DEFAULT now()::TEXT,
                    "Status"          TEXT    NOT NULL DEFAULT 'pending',
                    "ShippingCompany" TEXT    NULL,
                    "TrackingNumber"  TEXT    NULL,
                    CONSTRAINT "FK_SalesShipments_SalesOrders_SalesOrderId"
                        FOREIGN KEY ("SalesOrderId") REFERENCES "SalesOrders"("Id") ON DELETE CASCADE
                );
                """);

            // ── DeliveryItems ─────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "DeliveryItems" (
                    "Id"                   SERIAL  NOT NULL PRIMARY KEY,
                    "SalesShipmentId"      INTEGER NOT NULL,
                    "ProductId"            INTEGER NOT NULL,
                    "OrderedQuantity"      INTEGER NOT NULL DEFAULT 0,
                    "DeliveredQuantity"    INTEGER NOT NULL DEFAULT 0,
                    "MissingQuantity"      INTEGER NOT NULL DEFAULT 0,
                    "ExpectedDeliveryDate" TEXT    NULL,
                    "Note"                 TEXT    NULL,
                    "Status"               TEXT    NOT NULL DEFAULT 'pending',
                    CONSTRAINT "FK_DeliveryItems_SalesShipments_SalesShipmentId"
                        FOREIGN KEY ("SalesShipmentId") REFERENCES "SalesShipments"("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_DeliveryItems_Products_ProductId"
                        FOREIGN KEY ("ProductId") REFERENCES "Products"("Id") ON DELETE RESTRICT
                );
                """);

            // ── BrandCatalogs ─────────────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "BrandCatalogs" (
                    "Id"               SERIAL  NOT NULL PRIMARY KEY,
                    "BrandId"          INTEGER NOT NULL,
                    "OriginalFileName" TEXT    NOT NULL DEFAULT '',
                    "CustomFileName"   TEXT    NOT NULL DEFAULT '',
                    "FilePath"         TEXT    NOT NULL DEFAULT '',
                    "UploadedAt"       TEXT    NOT NULL DEFAULT now()::TEXT,
                    CONSTRAINT "FK_BrandCatalogs_Brands_BrandId"
                        FOREIGN KEY ("BrandId") REFERENCES "Brands"("Id") ON DELETE CASCADE
                );
                """);

            // ── ProductImportPreviews ─────────────────────────────────────────
            Execute(conn, """
                CREATE TABLE IF NOT EXISTS "ProductImportPreviews" (
                    "Id"          SERIAL  NOT NULL PRIMARY KEY,
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

    private static void Execute(NpgsqlConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
