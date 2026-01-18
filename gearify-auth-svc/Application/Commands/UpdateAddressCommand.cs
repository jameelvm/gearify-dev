using MediatR;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Command to update an existing address
/// </summary>
public record UpdateAddressCommand(
    string AddressId,
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
) : IRequest<UpdateAddressResult>;

public record UpdateAddressResult(bool Success, string? ErrorMessage = null);
