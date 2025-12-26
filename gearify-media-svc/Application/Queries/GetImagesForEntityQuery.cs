using Gearify.MediaService.Domain.Entities;
using MediatR;

namespace Gearify.MediaService.Application.Queries;

/// <summary>
/// Query to get all images for an entity (product, brand, etc.)
/// </summary>
public record GetImagesForEntityQuery(
    string EntityType,
    string EntityId,
    string TenantId
) : IRequest<List<MediaMetadata>>;
