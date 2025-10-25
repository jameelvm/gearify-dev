namespace Gearify.AuthService.API.DTOs;

/// <summary>
/// Response DTO containing authentication tokens and user information
/// </summary>
public record AuthResponse(
    string Token,
    string RefreshToken,
    UserDto User
);
