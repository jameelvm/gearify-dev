using Gearify.AuthService.API.DTOs;
using MediatR;

namespace Gearify.AuthService.Application.Queries;

/// <summary>
/// Query to get all addresses for a user
/// </summary>
public record GetUserAddressesQuery(string UserId, string TenantId) : IRequest<IEnumerable<AddressDto>>;
