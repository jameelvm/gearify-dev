using Gearify.CatalogService.Application.Services.Recommendations;
using Microsoft.AspNetCore.Mvc;

namespace Gearify.CatalogService.API.Controllers;

[ApiController]
[Route("api/recommendations")]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendationsService _recommendationsService;

    public RecommendationsController(IRecommendationsService recommendationsService)
    {
        _recommendationsService = recommendationsService;
    }

    [HttpGet("for-you")]
    public async Task<IActionResult> GetPersonalized(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var userId = Request.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(userId))
            return BadRequest(new { error = "X-User-Id header is required" });

        var result = await _recommendationsService.GetPersonalizedRecommendationsAsync(userId, limit, cancellationToken);
        return Ok(result);
    }

    [HttpGet("products/{productId}/similar")]
    public async Task<IActionResult> GetSimilar(
        string productId,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _recommendationsService.GetSimilarItemsAsync(productId, limit, cancellationToken);
        return Ok(result);
    }

    [HttpGet("products/{productId}/complementary")]
    public async Task<IActionResult> GetComplementary(
        string productId,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _recommendationsService.GetComplementaryItemsAsync(productId, limit, cancellationToken);
        return Ok(result);
    }

    [HttpPost("interactions")]
    public async Task<IActionResult> RecordInteraction(
        [FromBody] RecordInteractionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = Request.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(userId))
            return BadRequest(new { error = "X-User-Id header is required" });

        await _recommendationsService.RecordInteractionAsync(
            userId, request.ProductId, request.EventType, request.EventValue, cancellationToken);

        return Ok(new { status = "recorded" });
    }
}

public class RecordInteractionRequest
{
    public string ProductId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public decimal? EventValue { get; set; }
}
