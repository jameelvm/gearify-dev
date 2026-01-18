using Gearify.AuthService.API.DTOs;
using Gearify.AuthService.Infrastructure.Repositories;
using MediatR;

namespace Gearify.AuthService.Application.Queries;

/// <summary>
/// Handler for GetUserAddressesQuery
/// </summary>
public class GetUserAddressesQueryHandler : IRequestHandler<GetUserAddressesQuery, IEnumerable<AddressDto>>
{
    private readonly IAddressRepository _addressRepository;

    public GetUserAddressesQueryHandler(IAddressRepository addressRepository)
    {
        _addressRepository = addressRepository;
    }

    public async Task<IEnumerable<AddressDto>> Handle(GetUserAddressesQuery request, CancellationToken cancellationToken)
    {
        var addresses = await _addressRepository.GetByUserIdAsync(request.UserId, request.TenantId);

        return addresses.Select(a => new AddressDto(
            a.Id,
            a.Label,
            a.FirstName,
            a.LastName,
            a.Phone,
            a.AddressLine1,
            a.AddressLine2,
            a.City,
            a.State,
            a.ZipCode,
            a.Country,
            a.IsDefault
        ));
    }
}
