using System.Text.Json.Serialization;

namespace BizManager.Models;

public class BrandCatalog
{
    public int Id { get; set; }
    public int BrandId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string CustomFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public Brand? Brand { get; set; }
}
