using Gearify.CartService.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Gearify.CartService.API;

[ApiController]
[Route("api/cart")]
public class CartController : ControllerBase
{
    private readonly IMediator _mediator;

    public CartController(IMediator mediator) => _mediator = mediator;

    [HttpPost("{userId}/items")]
    public async Task<IActionResult> AddToCart(string userId, [FromBody] AddItemRequest request)
    {
        var cart = await _mediator.Send(new AddToCartCommand(userId, request.ProductId, request.Quantity, request.Price));
        return Ok(cart);
    }
}

public record AddItemRequest(string ProductId, int Quantity, decimal Price);
