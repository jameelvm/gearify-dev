using Gearify.MediaService.Domain.Entities;
using MediatR;

namespace Gearify.MediaService.Application.Queries;

/// <summary>
/// Query to get a single image by ID
/// </summary>
public record GetImageQuery(
    string MediaId,
    string TenantId
) : IRequest<MediaMetadata?>;
