using Gearify.MediaService.Domain.Entities;
using Gearify.MediaService.Infrastructure.Repositories;
using MediatR;

namespace Gearify.MediaService.Application.Queries;

/// <summary>
/// Handler for getting a single image
/// </summary>
public class GetImageQueryHandler : IRequestHandler<GetImageQuery, MediaMetadata?>
{
    private readonly IMediaRepository _mediaRepository;

    public GetImageQueryHandler(IMediaRepository mediaRepository)
    {
        _mediaRepository = mediaRepository;
    }

    public async Task<MediaMetadata?> Handle(GetImageQuery request, CancellationToken cancellationToken)
    {
        return await _mediaRepository.GetByIdAsync(request.MediaId, request.TenantId);
    }
}
