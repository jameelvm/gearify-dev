namespace Gearify.AuthService.API.DTOs;

/// <summary>
/// Request DTO for refreshing access token
/// </summary>
public record RefreshTokenRequest(
    string RefreshToken
);
