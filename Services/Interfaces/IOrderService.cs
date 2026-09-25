using ordersAPI.DTOs;
using ordersAPI.Entities;
namespace ordersAPI.Services;
public interface IOrderService
{
    Task<IEnumerable<Order>> GetAllOrdersAsync();
    Task<Order?> GetOrderByIdAsync(Guid id);
    Task<Order> CreateOrderAsync(CreateOrderRequest dto);
}

// // Testing
// public interface IFakeOrderService: IOrderService
// {
//     Task<IEnumerable<Order>> newNome();
//     Task<Order?> newNome4(Guid id);
//     Task<Order> newNome23(CreateOrderRequest dto);
// }