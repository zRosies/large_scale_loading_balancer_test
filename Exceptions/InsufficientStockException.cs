using ordersAPI.DTOs;

namespace ordersAPI.Exceptions;

public class InsufficientStockException : Exception
{
    public List<UnavailableItem> UnavailableItems { get; }

    public InsufficientStockException(List<UnavailableItem> unavailableItems)
        : base("Um ou mais produtos estão indisponíveis ou sem estoque suficiente.")
    {
        UnavailableItems = unavailableItems;
    }
}
