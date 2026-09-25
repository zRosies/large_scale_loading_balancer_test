using Microsoft.EntityFrameworkCore;
using ordersAPI.Database;
using ordersAPI.DTOs;
using ordersAPI.Entities;
using ordersAPI.Exceptions;

namespace ordersAPI.Services;

public class OrderService(OrdersDBContext orderDatabase, ProductDbContext productDatabase) : IOrderService
{
    private readonly OrdersDBContext _context = orderDatabase;
    private readonly ProductDbContext _productDatabase = productDatabase;

    public async Task<IEnumerable<Order>> GetAllOrdersAsync() =>
        await _context.Orders.AsNoTracking().ToListAsync();

    public async Task<Order?> GetOrderByIdAsync(Guid id) =>
        await _context.Orders.FindAsync(id);

    public async Task<Order> CreateOrderAsync(CreateOrderRequest orderPayload)
    {
        if (orderPayload.Items == null || orderPayload.Items.Count == 0)
        {
            throw new ArgumentException("O pedido precisa conter ao menos um item.");
        }

        var stockResult = await CheckStockAsync(orderPayload.Items.ToArray());

        if (!stockResult.Available)
        {
            throw new InsufficientStockException(stockResult.UnavailableItems);
        }

        decimal calculatedTotal = 0;
        foreach (var item in orderPayload.Items)
        {
            var product = stockResult.ProductsMap[item.ProductId];
            calculatedTotal += product.Price * item.Quantity;
            product.Stock -= item.Quantity;
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            // UserID = Get from the request token or session in a real application,
            // Hardcoded rn
            UserID = Guid.Parse("2b4c5f5f-5c30-4c28-bb3a-d5151b5c78eb"),
            TotalAmount = calculatedTotal,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Orders.Add(order);

        await _context.SaveChangesAsync();

        return order;
    }

    private async Task<StockCheckResult> CheckStockAsync(
        OrderItemRequest[] items)
    {
        var productIds = items
            .Select(i => i.ProductId)
            .ToArray();

        var products = await _productDatabase.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        var productMap = products.ToDictionary(p => p.Id);

        var unavailableItems = new List<UnavailableItem>();

        foreach (var item in items)
        {
            productMap.TryGetValue(item.ProductId, out var product);

            var unavailableItem = ValidateStock(item, product);

            if (unavailableItem is not null)
            {
                unavailableItems.Add(unavailableItem);
            }
        }

        return new StockCheckResult
        {
            Available = unavailableItems.Count == 0,
            UnavailableItems = unavailableItems,
            ProductsMap = productMap
        };
    }

    private UnavailableItem? ValidateStock(
        OrderItemRequest item,
        Product? product)
    {
        if (product is null)
        {
            return new UnavailableItem
            {
                ProductId = item.ProductId,
                Reason = "Product not found"
            };
        }

        if (!product.Active)
        {
            return new UnavailableItem
            {
                ProductId = item.ProductId,
                Reason = "Product is inactive"
            };
        }

        if (product.Stock < item.Quantity)
        {
            return new UnavailableItem
            {
                ProductId = item.ProductId,
                Requested = item.Quantity,
                Available = product.Stock,
                Reason = "Insufficient stock"
            };
        }

        return null;
    }
}
