using MediatR;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Command to delete an address
/// </summary>
public record DeleteAddressCommand(
    string AddressId,
    string UserId,
    string TenantId
) : IRequest<DeleteAddressResult>;

public record DeleteAddressResult(bool Success, string? ErrorMessage = null);
