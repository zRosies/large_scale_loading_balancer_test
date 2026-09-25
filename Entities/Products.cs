using System.ComponentModel.DataAnnotations.Schema;

namespace ordersAPI.Entities;

[Table("products")]
public class Product
{
    [Column("id")]
    public Guid Id { get; set; }
    [Column("name")]
    public string Name { get; set; } = string.Empty;
    [Column("price")]
    public decimal Price { get; set; }
    [Column("stock")]
    public int Stock { get; set; }
    [Column("active")]
    public bool Active { get; set; } = true;
}