using ordersAPI.Entities;
namespace ordersAPI.DTOs;
public class StockCheckResult
{
    public bool Available { get; set; }
    public List<UnavailableItem> UnavailableItems { get; set; } = [];
    public Dictionary<Guid, Product> ProductsMap { get; set; } = [];
}
public class UnavailableItem
{
    public Guid ProductId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int? Requested { get; set; }
    public int? Available { get; set; }
}