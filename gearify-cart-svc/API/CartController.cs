using System;
using System.Threading.Tasks;
using Gearify.CartService.API.Models;
using Gearify.CartService.Application.Commands;
using Gearify.CartService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Gearify.CartService.API;

[ApiController]
[Route("api/cart")]
public class CartController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<CartController> _logger;

    public CartController(IMediator mediator, ILogger<CartController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Generate a new guest cart ID (GUID)
    /// </summary>
    [HttpPost("guest/new")]
    public ActionResult<GuestCartResponse> CreateGuestCart()
    {
        var guestId = Guid.NewGuid();
        _logger.LogInformation("Generated new guest cart ID: {GuestId}", guestId);
        return Ok(new GuestCartResponse(guestId));
    }

    /// <summary>
    /// Get user's cart
    /// </summary>
    [HttpGet("{userId}")]
    public async Task<ActionResult<CartResponse>> GetCart(string userId)
    {
        var cart = await _mediator.Send(new GetCartQuery(userId));
        return Ok(cart);
    }

    /// <summary>
    /// Add item to cart
    /// </summary>
    [HttpPost("{userId}/items")]
    public async Task<ActionResult<CartResponse>> AddToCart(string userId, [FromBody] AddItemRequest request)
    {
        var result = await _mediator.Send(new AddToCartCommand(userId, request.ProductId, request.Quantity));

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Cart);
    }

    /// <summary>
    /// Update item quantity in cart
    /// </summary>
    [HttpPut("{userId}/items/{productId}")]
    public async Task<ActionResult<CartResponse>> UpdateCartItem(string userId, string productId, [FromBody] UpdateQuantityRequest request)
    {
        var result = await _mediator.Send(new UpdateCartItemCommand(userId, productId, request.Quantity));

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Cart);
    }

    /// <summary>
    /// Remove item from cart
    /// </summary>
    [HttpDelete("{userId}/items/{productId}")]
    public async Task<ActionResult<CartResponse>> RemoveFromCart(string userId, string productId)
    {
        var result = await _mediator.Send(new RemoveFromCartCommand(userId, productId));

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Cart);
    }

    /// <summary>
    /// Clear entire cart
    /// </summary>
    [HttpDelete("{userId}")]
    public async Task<IActionResult> ClearCart(string userId)
    {
        var result = await _mediator.Send(new ClearCartCommand(userId));

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Cart cleared" });
    }

    /// <summary>
    /// Merge guest cart into user cart
    /// </summary>
    [HttpPost("merge")]
    public async Task<ActionResult<CartResponse>> MergeCart([FromBody] MergeCartRequest request)
    {
        _logger.LogInformation(
            "Merge request: GuestCartId={GuestCartId}, UserId={UserId}, Strategy={Strategy}",
            request.GuestCartId, request.UserId, request.Strategy);

        var result = await _mediator.Send(new MergeCartCommand(
            request.GuestCartId,
            request.UserId,
            request.Strategy
        ));

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Cart);
    }
}
