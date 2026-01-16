using System;

namespace Gearify.CartService.API.Models;

public record AddItemRequest(
    string ProductId,
    int Quantity = 1
);

public record UpdateQuantityRequest(int Quantity);

public record MergeCartRequest(
    Guid GuestCartId,
    Guid UserId,
    MergeStrategy Strategy = MergeStrategy.Combine
);

public enum MergeStrategy
{
    Combine,           // Add guest items to user cart, sum quantities if same product
    ReplaceWithGuest,  // Delete user cart, keep only guest cart items
    KeepUser           // Ignore guest cart, keep only user cart
}
