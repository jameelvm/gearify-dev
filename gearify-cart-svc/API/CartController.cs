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
    public async Task<IActionResult> AddToCart(string userId, [FromBody] AddItemRequest request, [FromHeader(Name = "X-Tenant-Id")] string tenantId)
    {
        var result = await _mediator.Send(new AddToCartCommand(
            userId,
            tenantId,
            request.ProductId,
            request.ProductName,
            request.Sku,
            request.Quantity,
            request.Price,
            request.ImageUrl
        ));

        if (!result.Success)
            return BadRequest(result.ErrorMessage);

        return Ok(result.Cart);
    }
}

public record AddItemRequest(string ProductId, string ProductName, string Sku, int Quantity, decimal Price, string? ImageUrl = null);
