using Gearify.MediaService.API.DTOs;
using Gearify.MediaService.Application.Commands;
using Gearify.MediaService.Application.Queries;
using Gearify.MediaService.Application.Services;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Gearify.MediaService.API.Controllers;

/// <summary>
/// Media management endpoints
/// </summary>
[ApiController]
[Route("api/media")]
public class MediaController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;
    private readonly IMediaUrlFactory _urlFactory;
    private readonly ILogger<MediaController> _logger;

    public MediaController(
        IMediator mediator,
        ITenantContext tenantContext,
        IMediaUrlFactory urlFactory,
        ILogger<MediaController> logger)
    {
        _mediator = mediator;
        _tenantContext = tenantContext;
        _urlFactory = urlFactory;
        _logger = logger;
    }

    /// <summary>
    /// Upload a new image
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(MediaMetadataDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UploadImage(
        [FromForm] IFormFile? file,
        [FromForm] string entityType,
        [FromForm] string entityId,
        [FromForm] int displayOrder = 0,
        [FromForm] string? altText = null)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file provided" });
            }

            var tenantId = _tenantContext.TenantId;

            // Get user ID from claims (if authenticated)
            var uploadedBy = User.Identity?.Name ?? "system";

            using var stream = file.OpenReadStream();

            var command = new UploadImageCommand(
                FileStream: stream,
                FileName: file.FileName,
                ContentType: file.ContentType,
                SizeInBytes: file.Length,
                EntityType: entityType,
                EntityId: entityId,
                TenantId: tenantId,
                UploadedBy: uploadedBy,
                DisplayOrder: displayOrder,
                AltText: altText);

            var result = await _mediator.Send(command);

            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return Ok(new
            {
                mediaId = result.MediaId,
                urls = result.Urls
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get image metadata by ID
    /// </summary>
    [HttpGet("{mediaId}")]
    [ProducesResponseType(typeof(MediaMetadataDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetImage(string mediaId)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            var query = new GetImageQuery(mediaId, tenantId);
            var media = await _mediator.Send(query);

            if (media == null)
            {
                return NotFound(new { error = "Media not found" });
            }

            var urls = _urlFactory.GetUrls(media);

            var dto = new MediaMetadataDto(
                Id: media.Id,
                EntityType: media.EntityType,
                EntityId: media.EntityId,
                FileName: media.FileName,
                OriginalFileName: media.OriginalFileName,
                ContentType: media.ContentType,
                SizeInBytes: media.SizeInBytes,
                Width: media.Width,
                Height: media.Height,
                Urls: urls,
                DisplayOrder: media.DisplayOrder,
                AltText: media.AltText,
                Status: media.Status,
                ProcessedAt: media.ProcessedAt,
                ProcessingError: media.ProcessingError,
                UploadedAt: media.UploadedAt,
                UploadedBy: media.UploadedBy);

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting image {MediaId}", mediaId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get all images for an entity (product, brand, etc.)
    /// </summary>
    [HttpGet("{entityType}/{entityId}")]
    [ProducesResponseType(typeof(List<MediaMetadataDto>), 200)]
    public async Task<IActionResult> GetImagesForEntity(string entityType, string entityId)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            var query = new GetImagesForEntityQuery(entityType, entityId, tenantId);
            var mediaList = await _mediator.Send(query);

            var dtos = mediaList.Select(media =>
            {
                var urls = _urlFactory.GetUrls(media);

                return new MediaMetadataDto(
                    Id: media.Id,
                    EntityType: media.EntityType,
                    EntityId: media.EntityId,
                    FileName: media.FileName,
                    OriginalFileName: media.OriginalFileName,
                    ContentType: media.ContentType,
                    SizeInBytes: media.SizeInBytes,
                    Width: media.Width,
                    Height: media.Height,
                    Urls: urls,
                    DisplayOrder: media.DisplayOrder,
                    AltText: media.AltText,
                    Status: media.Status,
                    ProcessedAt: media.ProcessedAt,
                    ProcessingError: media.ProcessingError,
                    UploadedAt: media.UploadedAt,
                    UploadedBy: media.UploadedBy);
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting images for {EntityType} {EntityId}", entityType, entityId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete an image
    /// </summary>
    [HttpDelete("{mediaId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteImage(string mediaId, [FromQuery] bool hardDelete = false)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            var command = new DeleteImageCommand(mediaId, tenantId, hardDelete);
            var result = await _mediator.Send(command);

            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image {MediaId}", mediaId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
