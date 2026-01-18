using Gearify.AuthService.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Handler for UpdateAddressCommand
/// </summary>
public class UpdateAddressCommandHandler : IRequestHandler<UpdateAddressCommand, UpdateAddressResult>
{
    private readonly IAddressRepository _addressRepository;
    private readonly ILogger<UpdateAddressCommandHandler> _logger;

    public UpdateAddressCommandHandler(
        IAddressRepository addressRepository,
        ILogger<UpdateAddressCommandHandler> logger)
    {
        _addressRepository = addressRepository;
        _logger = logger;
    }

    public async Task<UpdateAddressResult> Handle(UpdateAddressCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var address = await _addressRepository.GetByIdAsync(request.AddressId, request.UserId, request.TenantId);

            if (address == null)
            {
                return new UpdateAddressResult(false, "Address not found");
            }

            // If setting as default, clear other defaults first
            if (request.IsDefault && !address.IsDefault)
            {
                await _addressRepository.ClearDefaultAsync(request.UserId, request.TenantId);
            }

            address.Label = request.Label;
            address.FirstName = request.FirstName;
            address.LastName = request.LastName;
            address.Phone = request.Phone;
            address.AddressLine1 = request.AddressLine1;
            address.AddressLine2 = request.AddressLine2;
            address.City = request.City;
            address.State = request.State;
            address.ZipCode = request.ZipCode;
            address.Country = request.Country;
            address.IsDefault = request.IsDefault;

            await _addressRepository.UpdateAsync(address);

            _logger.LogInformation("Updated address {AddressId} for user {UserId}", request.AddressId, request.UserId);

            return new UpdateAddressResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating address {AddressId}", request.AddressId);
            return new UpdateAddressResult(false, "Failed to update address");
        }
    }
}
