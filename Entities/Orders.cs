using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using NpgsqlTypes;

namespace ordersAPI.Entities;

[Table("orders")]
public class Order
{
    [Column("id")]
    public Guid Id { get; set; } 
    [Column("userId")]
    [JsonPropertyName("userId")]
    public Guid UserID { get; set; }
    [Column("total")]
    public decimal TotalAmount { get; set; }
    [Column("status")]
    public OrderStatus  Status { get; set; } = OrderStatus.Pending;
    [Column("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum OrderStatus
{
    [PgName("PENDING")]
    Pending,
    [PgName("CONFIRMED")]
    Confirmed,
    [PgName("CANCELLED")]
    Cancelled,
    [PgName("COMPLETED")]
    Completed
}