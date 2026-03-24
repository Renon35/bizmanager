using Microsoft.EntityFrameworkCore;
using BizManager.Models;

namespace BizManager.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Catalog> Catalogs => Set<Catalog>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<Dealer> Dealers => Set<Dealer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<DealerProduct> DealerProducts => Set<DealerProduct>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<SalesRep> SalesReps => Set<SalesRep>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<DealerInvoice> DealerInvoices => Set<DealerInvoice>();
    public DbSet<CustomerInvoice> CustomerInvoices => Set<CustomerInvoice>();
    public DbSet<BrandCatalog> BrandCatalogs => Set<BrandCatalog>();
    
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();
    public DbSet<SalesShipment> SalesShipments => Set<SalesShipment>();
    public DbSet<DeliveryItem> DeliveryItems => Set<DeliveryItem>();
    
    public DbSet<ProductImportPreview> ProductImportPreviews => Set<ProductImportPreview>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Brand>().Property(b => b.Name).IsRequired();
        modelBuilder.Entity<Catalog>().Property(c => c.CatalogName).IsRequired();
        modelBuilder.Entity<Dealer>().Property(d => d.Name).IsRequired();
        modelBuilder.Entity<Product>().Property(p => p.ProductName).IsRequired();
        modelBuilder.Entity<Product>().HasIndex(p => p.ProductCode);
        
        modelBuilder.Entity<PurchaseOrder>().Property(o => o.OrderNumber).IsRequired();
        modelBuilder.Entity<SalesRep>().Property(s => s.FirstName).IsRequired();
        modelBuilder.Entity<Customer>().Property(c => c.CompanyName).IsRequired();
        modelBuilder.Entity<Quotation>().Property(q => q.QuotationNumber).IsRequired();
        modelBuilder.Entity<SalesOrder>().Property(so => so.OrderNumber).IsRequired();

        // Quotation -> Dealer
        modelBuilder.Entity<Quotation>()
            .HasOne(q => q.Dealer)
            .WithMany()
            .HasForeignKey(q => q.DealerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Catalog -> Brand
        modelBuilder.Entity<Catalog>()
            .HasOne(c => c.Brand)
            .WithMany()
            .HasForeignKey(c => c.BrandId)
            .OnDelete(DeleteBehavior.Cascade);

        // Collection -> Catalog
        modelBuilder.Entity<Collection>()
            .HasOne(c => c.Catalog)
            .WithMany(cat => cat.Collections)
            .HasForeignKey(c => c.CatalogId)
            .OnDelete(DeleteBehavior.Cascade);

        // Product -> Catalog
        modelBuilder.Entity<Product>()
            .HasOne(p => p.Catalog)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CatalogId)
            .OnDelete(DeleteBehavior.SetNull);

        // Product -> Collection
        modelBuilder.Entity<Product>()
            .HasOne(p => p.Collection)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CollectionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Prevent cascade cycles
        modelBuilder.Entity<Sale>()
            .HasOne(s => s.Quotation)
            .WithOne(q => q.Sale)
            .HasForeignKey<Sale>(s => s.QuotationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Shipment>()
            .HasOne(s => s.Order)
            .WithOne(o => o.Shipment)
            .HasForeignKey<Shipment>(s => s.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DealerInvoice>()
            .HasOne(di => di.Order)
            .WithOne(o => o.DealerInvoice)
            .HasForeignKey<DealerInvoice>(di => di.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CustomerInvoice>()
            .HasOne(ci => ci.Sale)
            .WithOne(s => s.CustomerInvoice)
            .HasForeignKey<CustomerInvoice>(ci => ci.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DealerProduct>()
            .HasOne(dp => dp.Dealer)
            .WithMany(d => d.DealerProducts)
            .HasForeignKey(dp => dp.DealerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DealerProduct>()
            .HasOne(dp => dp.Product)
            .WithMany(p => p.DealerProducts)
            .HasForeignKey(dp => dp.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.Product)
            .WithMany()
            .HasForeignKey(oi => oi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BrandCatalog>()
            .HasOne(bc => bc.Brand)
            .WithMany(b => b.Catalogs)
            .HasForeignKey(bc => bc.BrandId)
            .OnDelete(DeleteBehavior.Cascade);

        // SalesOrder relationships
        modelBuilder.Entity<SalesOrderItem>()
            .HasOne(soi => soi.SalesOrder)
            .WithMany(so => so.Items)
            .HasForeignKey(soi => soi.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SalesOrderItem>()
            .HasOne(soi => soi.Product)
            .WithMany()
            .HasForeignKey(soi => soi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SalesShipment>()
            .HasOne(ss => ss.SalesOrder)
            .WithMany(so => so.Shipments)
            .HasForeignKey(ss => ss.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeliveryItem>()
            .HasOne(di => di.SalesShipment)
            .WithMany(ss => ss.DeliveryItems)
            .HasForeignKey(di => di.SalesShipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeliveryItem>()
            .HasOne(di => di.Product)
            .WithMany()
            .HasForeignKey(di => di.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
