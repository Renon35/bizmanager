namespace BizManager.Models.DTOs;

public class BulkDeleteRequest
{
    public List<int> ProductIds { get; set; } = new();
}

public class BulkUpdatePriceRequest
{
    public List<int> ProductIds { get; set; } = new();
    public decimal Percentage { get; set; } // e.g., 10 for +10%, -5 for -5%
}

public class BulkUpdateCollectionRequest
{
    public List<int> ProductIds { get; set; } = new();
    public int? NewCollectionId { get; set; }
}
