using Gearify.MediaService.Infrastructure.Repositories;
using Gearify.MediaService.Infrastructure.Storage;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.MediaService.Application.Commands;

/// <summary>
/// Handler for deleting images
/// </summary>
public class DeleteImageCommandHandler : IRequestHandler<DeleteImageCommand, DeleteImageResult>
{
    private readonly IStorageService _storageService;
    private readonly IMediaRepository _mediaRepository;
    private readonly ILogger<DeleteImageCommandHandler> _logger;

    public DeleteImageCommandHandler(
        IStorageService storageService,
        IMediaRepository mediaRepository,
        ILogger<DeleteImageCommandHandler> logger)
    {
        _storageService = storageService;
        _mediaRepository = mediaRepository;
        _logger = logger;
    }

    public async Task<DeleteImageResult> Handle(DeleteImageCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Get media metadata
            var media = await _mediaRepository.GetByIdAsync(request.MediaId, request.TenantId);

            if (media == null)
            {
                return new DeleteImageResult(
                    Success: false,
                    ErrorMessage: "Media not found");
            }

            if (request.HardDelete)
            {
                // Delete from S3
                var keys = new List<string>
                {
                    media.OriginalKey,
                    media.ThumbnailKey,
                    media.MediumKey,
                    media.LargeKey
                }.Where(k => !string.IsNullOrEmpty(k));

                await _storageService.DeleteMultipleAsync(keys, cancellationToken);

                // Hard delete from DynamoDB
                await _mediaRepository.HardDeleteAsync(request.MediaId, request.TenantId);

                _logger.LogInformation("Hard deleted media {MediaId} and all variants from S3", request.MediaId);
            }
            else
            {
                // Soft delete (mark as deleted in DynamoDB, keep S3 files)
                await _mediaRepository.DeleteAsync(request.MediaId, request.TenantId);

                _logger.LogInformation("Soft deleted media {MediaId}", request.MediaId);
            }

            return new DeleteImageResult(Success: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting media {MediaId}", request.MediaId);
            return new DeleteImageResult(
                Success: false,
                ErrorMessage: "Failed to delete image. Please try again.");
        }
    }
}
