using Gearify.MediaService.Domain.Entities;

namespace Gearify.MediaService.Infrastructure.Repositories;

/// <summary>
/// Repository for media metadata operations
/// </summary>
public interface IMediaRepository
{
    /// <summary>
    /// Get media by ID
    /// </summary>
    Task<MediaMetadata?> GetByIdAsync(string mediaId, string tenantId);

    /// <summary>
    /// Get all media for an entity (product, brand, etc.)
    /// </summary>
    Task<List<MediaMetadata>> GetByEntityAsync(string entityType, string entityId, string tenantId);

    /// <summary>
    /// Get multiple media items by IDs
    /// </summary>
    Task<List<MediaMetadata>> GetByIdsAsync(List<string> mediaIds, string tenantId);

    /// <summary>
    /// Create new media metadata
    /// </summary>
    Task CreateAsync(MediaMetadata media);

    /// <summary>
    /// Update media metadata
    /// </summary>
    Task UpdateAsync(MediaMetadata media);

    /// <summary>
    /// Delete media metadata (soft delete)
    /// </summary>
    Task DeleteAsync(string mediaId, string tenantId);

    /// <summary>
    /// Hard delete media metadata
    /// </summary>
    Task HardDeleteAsync(string mediaId, string tenantId);
}
