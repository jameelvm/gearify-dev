using System.Collections.Generic;

namespace Gearify.CartService.Infrastructure.Clients.DTOs;

public class ProductValidationResult
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; }
    public string? ThumbnailUrl { get; set; }
    public List<string> ImageUrls { get; set; } = new();
}
