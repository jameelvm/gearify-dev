using Gearify.AuthService.Application.Services;
using Gearify.AuthService.Domain.Entities;
using Gearify.AuthService.Domain.Enums;
using Gearify.AuthService.Infrastructure.Repositories;
using System.Security.Cryptography;

namespace Gearify.AuthService.Infrastructure.Services;

/// <summary>
/// Implementation of OTP (One-Time Password) service for Email/SMS
/// </summary>
public class OtpService : IOtpService
{
    private readonly IMfaCodeRepository _mfaCodeRepository;

    public OtpService(IMfaCodeRepository mfaCodeRepository)
    {
        _mfaCodeRepository = mfaCodeRepository;
    }

    public string GenerateCode()
    {
        // Generate a cryptographically secure random 6-digit code
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        rng.GetBytes(bytes);

        // Convert to number between 0 and 999999
        var number = BitConverter.ToUInt32(bytes, 0) % 1000000;

        // Format as 6-digit string with leading zeros
        return number.ToString("D6");
    }

    public async Task<MfaCode> StoreCodeAsync(string userId, string tenantId, string code, MfaMethod method, string purpose, int expiryMinutes)
    {
        var mfaCode = new MfaCode
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TenantId = tenantId,
            CodeHash = HashCode(code),
            Method = method,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes),
            IsUsed = false,
            AttemptCount = 0,
            Purpose = purpose
        };

        await _mfaCodeRepository.CreateAsync(mfaCode);

        return mfaCode;
    }

    public async Task<bool> VerifyCodeAsync(string userId, string code, string purpose)
    {
        var codeHash = HashCode(code);
        var mfaCode = await _mfaCodeRepository.GetByUserAndCodeAsync(userId, codeHash, purpose);

        if (mfaCode == null)
        {
            return false;
        }

        // Check if code has expired
        if (mfaCode.ExpiresAt < DateTime.UtcNow)
        {
            return false;
        }

        // Check if code has already been used
        if (mfaCode.IsUsed)
        {
            return false;
        }

        // Check if max attempts exceeded (prevent brute force)
        if (mfaCode.AttemptCount >= 3)
        {
            return false;
        }

        // Code is valid
        return true;
    }

    public async Task InvalidateCodeAsync(string userId, string code)
    {
        var codeHash = HashCode(code);
        var mfaCode = await _mfaCodeRepository.GetByUserAndCodeAsync(userId, codeHash, "");

        if (mfaCode != null)
        {
            mfaCode.IsUsed = true;
            await _mfaCodeRepository.UpdateAsync(mfaCode);
        }
    }

    public async Task<int> CleanupExpiredCodesAsync(string userId)
    {
        return await _mfaCodeRepository.DeleteExpiredCodesAsync(userId);
    }

    private static string HashCode(string code)
    {
        // Use BCrypt for hashing codes (same as passwords)
        return BCrypt.Net.BCrypt.HashPassword(code);
    }
}
