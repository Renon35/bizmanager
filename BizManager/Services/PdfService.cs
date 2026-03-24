using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using BizManager.Models;

namespace BizManager.Services;

public class PdfService
{
    public PdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateQuotationPdf(Quotation quotation)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10));

                page.Header().Element(ComposeHeader);
                page.Content().Element(c => ComposeContent(c, quotation));
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return doc.GeneratePdf();

        void ComposeHeader(IContainer container)
        {
            container.Row(row =>
            {
                // Logo if available
                if (!string.IsNullOrEmpty(quotation.SalesRep?.LogoPath))
                {
                    var logoFile = Path.Combine("wwwroot", quotation.SalesRep.LogoPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(logoFile))
                    {
                        row.ConstantItem(80).Image(logoFile);
                    }
                }

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("FİYAT TEKLİFİ").FontSize(22).Bold().FontColor("#1a56db");
                    col.Item().Text($"Teklif No: {quotation.QuotationNumber}").SemiBold();
                    col.Item().Text($"Tarih: {quotation.Date:dd.MM.yyyy}");
                    col.Item().Text($"Müşteri: {quotation.Customer?.CompanyName}");
                });

                row.ConstantItem(160).Column(col =>
                {
                    col.Item().Text("Satış Temsilcisi").SemiBold().FontColor("#6b7280");
                    col.Item().Text($"{quotation.SalesRep?.FirstName} {quotation.SalesRep?.LastName}").Bold();
                    col.Item().Text(quotation.SalesRep?.Phone ?? "");
                    col.Item().Text(quotation.SalesRep?.Email ?? "");
                });
            });
        }

        void ComposeContent(IContainer container, Quotation q)
        {
            container.Column(col =>
            {
                col.Spacing(12);

                // Customer info
                col.Item().Background("#f3f4f6").Padding(12).Column(info =>
                {
                    info.Item().Text("MÜŞTERİ BİLGİLERİ").Bold().FontColor("#374151");
                    info.Item().Text($"Firma: {q.Customer?.CompanyName}").SemiBold();
                    info.Item().Text($"Yetkili: {q.Customer?.Representative}");
                    info.Item().Text($"Telefon: {q.Customer?.Phone}");
                    info.Item().Text($"E-Posta: {q.Customer?.Email}");
                    info.Item().Text($"Vergi No: {q.Customer?.TaxNumber}");
                });

                // Items table
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(50); // Image Column
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(1);
                        cols.ConstantColumn(60);
                        cols.ConstantColumn(80);
                        cols.ConstantColumn(80);
                    });

                    // Header
                    table.Header(header =>
                    {
                        void Cell(string text) =>
                            header.Cell().Background("#1a56db").Padding(6)
                                  .Text(text).Bold().FontColor(Colors.White).FontSize(9);
                        Cell("Görsel"); Cell("Ürün Adı"); Cell("Kod"); Cell("Adet"); Cell("Birim Fiyat"); Cell("Toplam");
                    });

                    // Rows
                    var alt = false;
                    foreach (var item in q.Items)
                    {
                        var bg = (alt = !alt) ? "#ffffff" : "#f9fafb";

                        // Image Cell
                        var cellFormat = table.Cell().Background(bg).Padding(5);
                        if (!string.IsNullOrEmpty(item.ImageUrl))
                        {
                            var imgPath = Path.Combine("wwwroot", item.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                            if (File.Exists(imgPath))
                            {
                                cellFormat.Image(imgPath);
                            }
                            else
                            {
                                cellFormat.Text("");
                            }
                        }
                        else
                        {
                            cellFormat.Text("");
                        }

                        table.Cell().Background(bg).Padding(5).Text(item.ProductName);
                        table.Cell().Background(bg).Padding(5).Text(item.ProductCode ?? "-");
                        table.Cell().Background(bg).Padding(5).AlignCenter().Text(item.Quantity.ToString());
                        table.Cell().Background(bg).Padding(5).AlignRight().Text($"{item.UnitPrice:N2}");
                        table.Cell().Background(bg).Padding(5).AlignRight().Text($"{item.TotalPrice:N2}");
                    }
                });

                // Total
                col.Item().AlignRight().Column(total =>
                {
                    total.Item().BorderTop(1).BorderColor("#d1d5db").PaddingTop(6).PaddingBottom(2)
                        .Row(r =>
                        {
                            r.RelativeItem().Text("Ara Toplam:").Bold().FontSize(11).FontColor("#6b7280");
                            r.ConstantItem(100).AlignRight().Text($"{q.Subtotal:N2}").Bold().FontSize(11).FontColor("#374151");
                        });
                        
                    total.Item().PaddingBottom(2)
                        .Row(r =>
                        {
                            r.RelativeItem().Text($"KDV (%{q.VatRate}):").Bold().FontSize(11).FontColor("#6b7280");
                            r.ConstantItem(100).AlignRight().Text($"{q.VatAmount:N2}").Bold().FontSize(11).FontColor("#374151");
                        });

                    total.Item().BorderTop(1).BorderColor("#d1d5db").PaddingTop(4)
                        .Row(r =>
                        {
                            r.RelativeItem().Text("Genel Toplam:").Bold().FontSize(13);
                            r.ConstantItem(100).AlignRight().Text($"{q.GrandTotal:N2}").Bold().FontSize(13).FontColor("#1a56db");
                        });
                });

                col.Item().PaddingTop(20).Text("Şartlar ve Koşullar").Bold().FontColor("#6b7280");
                col.Item().Text("Fiyatlar teklif tarihinden itibaren 30 gün boyunca geçerlidir.")
                    .FontColor("#9ca3af").FontSize(8);
            });
        }
    }

    public byte[] GenerateProductListPdf(List<Product> products, string brandName, string? catalogName)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9));

                page.Header().Element(ComposeHeader);
                page.Content().Element(c => ComposeContent(c, products));
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Sayfa ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        return doc.GeneratePdf();

        void ComposeHeader(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("ÜRÜN LİSTESİ").FontSize(18).Bold().FontColor("#1a56db");
                    col.Item().Text($"Marka: {brandName}").SemiBold();
                    if (!string.IsNullOrEmpty(catalogName))
                    {
                        col.Item().Text($"Katalog: {catalogName}");
                    }
                });

                row.ConstantItem(150).AlignRight().Column(col =>
                {
                    col.Item().Text($"Tarih: {DateTime.Now:dd.MM.yyyy}").SemiBold().FontColor("#6b7280");
                    col.Item().Text($"Toplam Ürün: {products.Count}").Bold();
                });
            });
        }

        void ComposeContent(IContainer container, List<Product> pList)
        {
            container.PaddingTop(15).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(40); // Image
                    cols.RelativeColumn(2);  // Collection
                    cols.RelativeColumn(3);  // Product Name
                    cols.RelativeColumn(2);  // Mold Code
                    cols.RelativeColumn(2);  // Product Code
                    cols.ConstantColumn(60); // Price
                    cols.ConstantColumn(50); // Stock
                });

                // Header
                table.Header(header =>
                {
                    void Cell(string text) =>
                        header.Cell().Background("#1a56db").Padding(5)
                              .Text(text).Bold().FontColor(Colors.White).FontSize(8);

                    Cell("Görsel"); Cell("Koleksiyon"); Cell("Ürün Adı"); Cell("Kalıp Kodu"); Cell("Ürün Kodu"); Cell("Fiyat"); Cell("Stok");
                });

                // Rows
                var alt = false;
                foreach (var item in pList)
                {
                    var bg = (alt = !alt) ? "#ffffff" : "#f9fafb";

                    // Image Cell
                    var imgCell = table.Cell().Background(bg).Padding(4);
                    if (!string.IsNullOrEmpty(item.ImageUrl))
                    {
                        var imgPath = Path.Combine("wwwroot", item.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(imgPath))
                        {
                            try
                            {
                                imgCell.Height(30).Image(imgPath);
                            }
                            catch
                            {
                                imgCell.AlignCenter().AlignMiddle().Text("Hata").FontSize(7).FontColor(Colors.Red.Medium);
                            }
                        }
                        else
                        {
                            imgCell.AlignCenter().AlignMiddle().Text("Yok").FontSize(7).FontColor(Colors.Grey.Medium);
                        }
                    }
                    else
                    {
                        imgCell.AlignCenter().AlignMiddle().Text("Yok").FontSize(7).FontColor(Colors.Grey.Medium);
                    }

                    void DataCell(string? text, bool right = false)
                    {
                        var c = table.Cell().Background(bg).Padding(4).AlignMiddle();
                        if (right) c.AlignRight();
                        c.Text(text ?? "-").FontSize(8);
                    }

                    DataCell(item.Collection?.CollectionName ?? "-");
                    DataCell(item.ProductName);
                    DataCell(item.MoldCode);
                    DataCell(item.ProductCode);
                    
                    // Prioritize list price, then sale price, then purchase price in list view usually, or just show sale price
                    decimal price = item.ListPrice > 0 ? item.ListPrice : (item.SalePrice > 0 ? item.SalePrice : item.PurchasePrice);
                    DataCell($"{price:N2} ₺", true);

                    // Combine stock from dealers if any, else 0
                    int totalStock = item.DealerProducts?.Sum(dp => dp.StockQuantity) ?? 0;
                    DataCell(totalStock.ToString(), true);
                }
            });
        }
    }
}
