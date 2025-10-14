using Microsoft.AspNetCore.Mvc;
using Gearify.CatalogService.Models;

namespace Gearify.CatalogService.Controllers;

[ApiController]
[Route("api/catalog")]
public class ProductsController : ControllerBase
{
    private static readonly List<Product> Products = new()
    {
        new() { Id = "1", Name = "CA Plus 15000 Bat", Category = "bat", Price = 299.99m, Brand = "CA", Description = "Premium English willow bat" },
        new() { Id = "2", Name = "SG RSD Xtreme Bat", Category = "bat", Price = 349.99m, Brand = "SG", Description = "Power hitting bat" },
        new() { Id = "3", Name = "GM Diamond Bat", Category = "bat", Price = 279.99m, Brand = "GM", Description = "Lightweight bat" },
        new() { Id = "4", Name = "Kookaburra Ghost Pro Bat", Category = "bat", Price = 399.99m, Brand = "Kookaburra", Description = "Professional grade" },
        new() { Id = "5", Name = "SG Test Pads", Category = "pad", Price = 79.99m, Brand = "SG", Description = "Protective pads" },
        new() { Id = "6", Name = "CA Gloves", Category = "glove", Price = 59.99m, Brand = "CA", Description = "Premium gloves" },
        new() { Id = "7", Name = "Kookaburra Ball", Category = "ball", Price = 19.99m, Brand = "Kookaburra", Description = "Match ball" }
    };

    [HttpGet("products")]
    public IActionResult GetProducts([FromQuery] string? category)
    {
        var products = string.IsNullOrEmpty(category)
            ? Products
            : Products.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();

        return Ok(products);
    }

    [HttpGet("products/{id}")]
    public IActionResult GetProduct(string id)
    {
        var product = Products.FirstOrDefault(p => p.Id == id);
        return product == null ? NotFound() : Ok(product);
    }

    [HttpPost("products")]
    public IActionResult CreateProduct([FromBody] Product product)
    {
        Products.Add(product);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }
}
