using MediatR;

namespace Gearify.MediaService.Application.Commands;

/// <summary>
/// Command to delete an image
/// </summary>
public record DeleteImageCommand(
    string MediaId,
    string TenantId,
    bool HardDelete = false
) : IRequest<DeleteImageResult>;

/// <summary>
/// Result of image deletion
/// </summary>
public record DeleteImageResult(
    bool Success,
    string? ErrorMessage = null
);
