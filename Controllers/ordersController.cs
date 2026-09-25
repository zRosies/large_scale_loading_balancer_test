using Microsoft.AspNetCore.Mvc;
using ordersAPI.DTOs;
using ordersAPI.Entities;
using ordersAPI.Exceptions;
using ordersAPI.Services;

namespace ordersAPI.Controllers;

[ApiController] // Habilita validações automáticas do modelo e respostas 400 em caso de erro
[Route("api/orders")] // Define a rota base: "api/orders" (remove o sufixo "Controller")
public class OrdersController(IOrderService OrderService) : ControllerBase
{
    // GET: api/orders
    // Return all orders
    [HttpGet]
    public async Task<ActionResult<List<Order>>> GetAll()
    {
        return Ok(await OrderService.GetAllOrdersAsync());
    }

    // GET: api/orders/5
    [HttpGet("{id:guid}")] // {id:int} restringe o parâmetro para número inteiro
    public async Task<ActionResult<Order?>> GetById([FromRoute] Guid id)
    {
        // [FromRoute] pega o valor da própria URL
        // if (id <= 0)
        //     return NotFound("Pedido não encontrado");

        return Ok(await OrderService.GetOrderByIdAsync(id));
    }

    // POST: api/orders
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        try
        {
            var createdOrder = await OrderService.CreateOrderAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = createdOrder.Id }, createdOrder);
        }
        catch (InsufficientStockException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Insufficient stock",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict,
                Extensions =
                {
                    ["unavailableItems"] = ex.UnavailableItems
                }
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    // // PUT: api/orders/5
    // [HttpPut("{id:int}")]
    // public IActionResult Update(int id, [FromBody] UpdateOrderDto dto)
    // {
    //     return NoContent(); // HTTP 204
    // }

    // // DELETE: api/orders/5
    // [HttpDelete("{id:int}")]
    // public IActionResult Delete(int id)
    // {
    //     return NoContent(); // HTTP 204
    // }
}

