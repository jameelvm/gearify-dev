using Gearify.AuthService.Application.Models;
using Gearify.AuthService.Application.Services;
using Gearify.AuthService.Domain.Enums;
using Gearify.AuthService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Handles TOTP MFA setup
/// </summary>
public class SetupTotpMfaCommandHandler : IRequestHandler<SetupTotpMfaCommand, MfaSetupResult>
{
    private readonly IUserRepository _userRepository;
    private readonly ITotpService _totpService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<SetupTotpMfaCommandHandler> _logger;
    private readonly MfaSettings _mfaSettings;

    public SetupTotpMfaCommandHandler(
        IUserRepository userRepository,
        ITotpService totpService,
        ITenantContext tenantContext,
        ILogger<SetupTotpMfaCommandHandler> logger,
        IOptions<SecurityConfiguration> securityConfig)
    {
        _userRepository = userRepository;
        _totpService = totpService;
        _tenantContext = tenantContext;
        _logger = logger;
        _mfaSettings = securityConfig.Value.Mfa;
    }

    public async Task<MfaSetupResult> Handle(SetupTotpMfaCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Get user
            var user = await _userRepository.GetByIdAsync(request.UserId, tenantId);
            if (user == null)
            {
                return new MfaSetupResult
                {
                    Success = false,
                    Message = "User not found."
                };
            }

            // Generate TOTP secret
            var secret = _totpService.GenerateSecret();

            // Generate QR code
            var qrCode = _totpService.GenerateQrCode(user.Email, secret, _mfaSettings.TotpIssuer);

            // Generate backup codes
            var backupCodes = GenerateBackupCodes(_mfaSettings.BackupCodesCount);

            // Hash backup codes before storing
            var hashedBackupCodes = backupCodes.Select(code => BCrypt.Net.BCrypt.HashPassword(code)).ToArray();

            // Update user with TOTP secret and backup codes (but don't enable MFA yet)
            // MFA will be enabled after verification
            user.TotpSecret = secret;
            user.BackupCodes = string.Join(",", hashedBackupCodes);
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user);

            _logger.LogInformation("TOTP MFA setup initiated for user {UserId}", user.Id);

            return new MfaSetupResult
            {
                Success = true,
                Message = "TOTP MFA setup successful. Please scan the QR code with your authenticator app.",
                QrCodeBase64 = qrCode,
                ManualEntryKey = _totpService.FormatSecretForDisplay(secret),
                BackupCodes = backupCodes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup TOTP MFA for user {UserId}", request.UserId);
            return new MfaSetupResult
            {
                Success = false,
                Message = "Failed to setup MFA. Please try again later."
            };
        }
    }

    private static string[] GenerateBackupCodes(int count)
    {
        var codes = new string[count];
        var random = new Random();

        for (int i = 0; i < count; i++)
        {
            // Generate 8-character alphanumeric backup code
            codes[i] = GenerateBackupCode(random);
        }

        return codes;
    }

    private static string GenerateBackupCode(Random random)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Excluding ambiguous characters
        var code = new char[8];

        for (int i = 0; i < 8; i++)
        {
            code[i] = chars[random.Next(chars.Length)];
        }

        // Format as XXXX-XXXX for readability
        return $"{new string(code, 0, 4)}-{new string(code, 4, 4)}";
    }
}
