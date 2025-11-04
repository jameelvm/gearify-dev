using Gearify.AuthService.Domain.Entities;

namespace Gearify.AuthService.Infrastructure.Repositories;

/// <summary>
/// Repository for MFA code operations
/// </summary>
public interface IMfaCodeRepository
{
    /// <summary>
    /// Creates a new MFA code
    /// </summary>
    Task CreateAsync(MfaCode code);

    /// <summary>
    /// Gets an MFA code by user ID and code value
    /// </summary>
    Task<MfaCode?> GetByUserAndCodeAsync(string userId, string codeHash, string purpose);

    /// <summary>
    /// Gets all active codes for a user
    /// </summary>
    Task<List<MfaCode>> GetActiveCodesAsync(string userId);

    /// <summary>
    /// Updates an MFA code
    /// </summary>
    Task UpdateAsync(MfaCode code);

    /// <summary>
    /// Deletes expired codes for a user
    /// </summary>
    Task<int> DeleteExpiredCodesAsync(string userId);

    /// <summary>
    /// Deletes all codes for a user
    /// </summary>
    Task DeleteAllUserCodesAsync(string userId);
}
