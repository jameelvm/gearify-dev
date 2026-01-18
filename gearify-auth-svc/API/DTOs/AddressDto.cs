namespace Gearify.AuthService.API.DTOs;

/// <summary>
/// Address data transfer object
/// </summary>
public record AddressDto(
    string Id,
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
);

/// <summary>
/// Request to create a new address
/// </summary>
public record CreateAddressRequest(
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
    bool IsDefault = false
);

/// <summary>
/// Request to update an existing address
/// </summary>
public record UpdateAddressRequest(
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
);
