using Gearify.AuthService.Application.Services;
using OtpNet;
using QRCoder;
using System.Text;

namespace Gearify.AuthService.Infrastructure.Services;

/// <summary>
/// Implementation of TOTP (Time-based One-Time Password) service
/// </summary>
public class TotpService : ITotpService
{
    public string GenerateSecret()
    {
        // Generate a random 20-byte secret (160 bits) for strong security
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    public string GenerateQrCode(string email, string secret, string issuer = "Gearify")
    {
        // Create otpauth URL for authenticator apps
        var otpAuthUrl = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}";

        // Generate QR code
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(otpAuthUrl, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);

        var qrCodeBytes = qrCode.GetGraphic(20);
        return Convert.ToBase64String(qrCodeBytes);
    }

    public bool VerifyCode(string secret, string code, int timeToleranceSeconds = 30)
    {
        try
        {
            // Decode the Base32 secret
            var secretBytes = Base32Encoding.ToBytes(secret);

            // Create TOTP instance
            var totp = new Totp(secretBytes, step: 30); // 30-second time step (standard)

            // Verify with time window tolerance
            var window = new VerificationWindow(
                previous: timeToleranceSeconds / 30,  // Number of 30-second windows to check before
                future: timeToleranceSeconds / 30     // Number of 30-second windows to check after
            );

            return totp.VerifyTotp(code, out _, window);
        }
        catch
        {
            return false;
        }
    }

    public string FormatSecretForDisplay(string secret)
    {
        // Format secret in groups of 4 for easier manual entry
        // Example: JBSW Y3DP EHPK 3PXP
        var sb = new StringBuilder();
        for (int i = 0; i < secret.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                sb.Append(' ');
            }
            sb.Append(secret[i]);
        }
        return sb.ToString();
    }
}
