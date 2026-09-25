namespace ordersAPI.DTOs;

public class CreateOrderRequest
{
    public List<OrderItemRequest> Items { get; set; } = [];

    public string UserId { get; set; } = string.Empty;
}

public class OrderItemRequest
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
