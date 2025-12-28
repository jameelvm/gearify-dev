namespace Gearify.MediaService.Domain.Enums;

/// <summary>
/// Processing status for media files
/// </summary>
public enum ProcessingStatus
{
    /// <summary>
    /// Original uploaded, variants being generated
    /// </summary>
    Processing = 0,

    /// <summary>
    /// All variants generated and ready
    /// </summary>
    Ready = 1,

    /// <summary>
    /// Processing failed
    /// </summary>
    Failed = 2
}
