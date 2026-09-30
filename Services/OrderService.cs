using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ordersAPI.Database;
using ordersAPI.DTOs;
using ordersAPI.Entities;
using ordersAPI.Exceptions;

namespace ordersAPI.Services;

public class OrderService(
    OrdersDBContext orderDatabase,
    ProductDbContext productDatabase,
    IDistributedCache cache,
    IMemoryCache memoryCache,
    ILogger<OrderService> logger) : IOrderService
{
    private readonly OrdersDBContext _context = orderDatabase;
    private readonly ProductDbContext _productDatabase = productDatabase;
    private readonly IDistributedCache _cache = cache;
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly ILogger<OrderService> _logger = logger;

    private const string AllOrdersCacheKey = "orders:all";
    private static readonly SemaphoreSlim _allOrdersLock = new(1, 1);

    public async Task<IEnumerable<Order>> GetAllOrdersAsync()
    {
        // 1. Ultra-fast L1 In-Memory Cache (sub-millisecond, absorbs concurrency bursts)
        if (_memoryCache.TryGetValue(AllOrdersCacheKey, out List<Order>? memOrders) && memOrders != null)
        {
            return memOrders;
        }

        // 2. Stampede Protection (Request Coalescing): Only one thread queries DB/Redis when cache is cold
        await _allOrdersLock.WaitAsync();
        try
        {
            // Double-check L1 after acquiring lock
            if (_memoryCache.TryGetValue(AllOrdersCacheKey, out memOrders) && memOrders != null)
            {
                return memOrders;
            }

            // 3. Try L2 Redis Distributed Cache with graceful fallback
            try
            {
                string? cachedOrders = await _cache.GetStringAsync(AllOrdersCacheKey);
                if (!string.IsNullOrEmpty(cachedOrders))
                {
                    var ordersFromRedis = JsonSerializer.Deserialize<List<Order>>(cachedOrders);
                    if (ordersFromRedis != null)
                    {
                        // Cache in L1 memory for 15 seconds
                        _memoryCache.Set(AllOrdersCacheKey, ordersFromRedis, TimeSpan.FromSeconds(15));
                        return ordersFromRedis;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis distributed cache read failed. Falling back to database.");
            }

            // 4. Cache Miss: Fetch from Postgres database
            var orders = await _context.Orders
                .AsNoTracking()
                .ToListAsync();

            // Populate L1 memory cache (15 seconds)
            _memoryCache.Set(AllOrdersCacheKey, orders, TimeSpan.FromSeconds(15));

            // Populate L2 Redis cache with graceful fallback
            try
            {
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromMinutes(2)
                };

                await _cache.SetStringAsync(
                    AllOrdersCacheKey,
                    JsonSerializer.Serialize(orders),
                    options
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis distributed cache write failed.");
            }

            return orders;
        }
        finally
        {
            _allOrdersLock.Release();
        }
    }


    public async Task<Order?> GetOrderByIdAsync(Guid id)
    {
        string cacheKey = $"order:{id}";

        // 1. Try L1 Memory Cache
        if (_memoryCache.TryGetValue(cacheKey, out Order? memOrder) && memOrder != null)
        {
            return memOrder;
        }

        // 2. Try L2 Redis with graceful fallback
        try
        {
            var cachedOrder = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedOrder))
            {
                var orderFromRedis = JsonSerializer.Deserialize<Order>(cachedOrder);
                if (orderFromRedis != null)
                {
                    _memoryCache.Set(cacheKey, orderFromRedis, TimeSpan.FromMinutes(1));
                    return orderFromRedis;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache read failed for order {OrderId}. Falling back to database.", id);
        }

        // 3. Cache Miss: Fetch from Database
        var order = await _context.Orders.FindAsync(id);

        if (order != null)
        {
            _memoryCache.Set(cacheKey, order, TimeSpan.FromMinutes(1));

            try
            {
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromMinutes(2)
                };
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(order), options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis cache write failed for order {OrderId}.", id);
            }
        }

        return order;
    }


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
        await _productDatabase.SaveChangesAsync();

        // Invalidate orders list cache across L1 and L2
        _memoryCache.Remove(AllOrdersCacheKey);
        try
        {
            await _cache.RemoveAsync(AllOrdersCacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate Redis cache for {CacheKey}.", AllOrdersCacheKey);
        }

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
