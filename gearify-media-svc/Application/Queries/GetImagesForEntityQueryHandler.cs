using Gearify.MediaService.Domain.Entities;
using Gearify.MediaService.Infrastructure.Repositories;
using MediatR;

namespace Gearify.MediaService.Application.Queries;

/// <summary>
/// Handler for getting all images for an entity
/// </summary>
public class GetImagesForEntityQueryHandler : IRequestHandler<GetImagesForEntityQuery, List<MediaMetadata>>
{
    private readonly IMediaRepository _mediaRepository;

    public GetImagesForEntityQueryHandler(IMediaRepository mediaRepository)
    {
        _mediaRepository = mediaRepository;
    }

    public async Task<List<MediaMetadata>> Handle(GetImagesForEntityQuery request, CancellationToken cancellationToken)
    {
        return await _mediaRepository.GetByEntityAsync(request.EntityType, request.EntityId, request.TenantId);
    }
}
