namespace Gearify.CartService.API.Models;

public record AddItemRequest(
    string ProductId,
    int Quantity = 1
);

public record UpdateQuantityRequest(int Quantity);
