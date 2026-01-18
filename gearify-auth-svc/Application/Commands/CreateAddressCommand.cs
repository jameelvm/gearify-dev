using MediatR;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Command to create a new address
/// </summary>
public record CreateAddressCommand(
    string UserId,
    string TenantId,
    string Label,
    string FirstName,
    string LastName,
    string Phone,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string ZipCode,
    string Country,
    bool IsDefault
) : IRequest<CreateAddressResult>;

public record CreateAddressResult(bool Success, string? AddressId = null, string? ErrorMessage = null);
