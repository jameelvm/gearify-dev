using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gearify.CartService.API.Models;
using Gearify.CartService.Application.Commands;
using Gearify.CartService.Application.Queries;
using Gearify.CartService.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Gearify.CartService.API;

[ApiController]
[Route("api/cart")]
public class CartController : ControllerBase
{
    private readonly IMediator _mediator;

    public CartController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Get user's cart
    /// </summary>
    [HttpGet("{userId}")]
    public async Task<ActionResult<CartResponse>> GetCart(string userId)
    {
        var cart = await _mediator.Send(new GetCartQuery(userId));
        if (cart == null)
        {
            return Ok(new CartResponse(
                Id: string.Empty,
                UserId: userId,
                Items: new List<CartItemResponse>(),
                Subtotal: 0m,
                Total: 0m,
                Currency: "USD",
                ItemCount: 0,
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow
            ));
        }

        return Ok(MapToResponse(cart));
    }

    /// <summary>
    /// Add item to cart
    /// </summary>
    [HttpPost("{userId}/items")]
    public async Task<ActionResult<CartResponse>> AddToCart(string userId, [FromBody] AddItemRequest request)
    {
        var result = await _mediator.Send(new AddToCartCommand(
            userId,
            request.ProductId,
            request.Quantity
        ));

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(MapToResponse(result.Cart!));
    }

    /// <summary>
    /// Update item quantity in cart
    /// </summary>
    [HttpPut("{userId}/items/{productId}")]
    public async Task<ActionResult<CartResponse>> UpdateCartItem(string userId, string productId, [FromBody] UpdateQuantityRequest request)
    {
        var result = await _mediator.Send(new UpdateCartItemCommand(
            userId,
            productId,
            request.Quantity
        ));

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(MapToResponse(result.Cart!));
    }

    /// <summary>
    /// Remove item from cart
    /// </summary>
    [HttpDelete("{userId}/items/{productId}")]
    public async Task<IActionResult> RemoveFromCart(string userId, string productId)
    {
        var result = await _mediator.Send(new RemoveFromCartCommand(userId, productId));

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Item removed from cart" });
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

    private static CartResponse MapToResponse(Cart cart)
    {
        var items = cart.Items.Select(i => new CartItemResponse(
            ProductId: i.ProductId,
            ProductName: i.ProductName,
            Sku: i.Sku,
            ImageUrl: i.ImageUrl,
            Quantity: i.Quantity,
            UnitPrice: i.Price,
            LineTotal: i.Price * i.Quantity
        )).ToList();

        return new CartResponse(
            Id: cart.Id,
            UserId: cart.UserId,
            Items: items,
            Subtotal: cart.TotalAmount,
            Total: cart.TotalAmount,
            Currency: cart.Currency,
            ItemCount: cart.Items.Count,
            CreatedAt: cart.CreatedAt,
            UpdatedAt: cart.UpdatedAt
        );
    }
}
