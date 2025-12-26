using MediatR;
using Microsoft.AspNetCore.Http;

namespace Gearify.CatalogService.Application.Commands;

/// <summary>
/// Command to upload images for a product
/// </summary>
public record UploadProductImagesCommand(
    string ProductId,
    List<IFormFile> Images,
    List<string>? AltTexts = null
) : IRequest<UploadProductImagesResult>;

public record UploadProductImagesResult(
    bool Success,
    List<UploadedImageInfo>? UploadedImages = null,
    string? ErrorMessage = null);

public record UploadedImageInfo(
    string MediaId,
    Dictionary<string, string> Urls,
    int DisplayOrder);
