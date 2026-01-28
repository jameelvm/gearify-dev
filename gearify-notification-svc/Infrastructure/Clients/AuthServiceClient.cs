using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Gearify.NotificationService.Infrastructure.Clients;

public interface IAuthServiceClient
{
    Task<UserInfo?> GetUserByIdAsync(string userId, string tenantId, CancellationToken ct = default);
}

public class AuthServiceClient : IAuthServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthServiceClient> _logger;

    public AuthServiceClient(HttpClient httpClient, ILogger<AuthServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<UserInfo?> GetUserByIdAsync(string userId, string tenantId, CancellationToken ct = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/user/{userId}");
            request.Headers.Add("X-Tenant-Id", tenantId);

            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get user {UserId} from Auth Service: {StatusCode}",
                    userId, response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<UserInfo>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Auth Service for user {UserId}", userId);
            return null;
        }
    }
}

public record UserInfo
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? Phone { get; init; }
}
