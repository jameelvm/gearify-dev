using Gearify.AuthService.Domain.Entities;
using Gearify.AuthService.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Handler for CreateAddressCommand
/// </summary>
public class CreateAddressCommandHandler : IRequestHandler<CreateAddressCommand, CreateAddressResult>
{
    private readonly IAddressRepository _addressRepository;
    private readonly ILogger<CreateAddressCommandHandler> _logger;

    public CreateAddressCommandHandler(
        IAddressRepository addressRepository,
        ILogger<CreateAddressCommandHandler> logger)
    {
        _addressRepository = addressRepository;
        _logger = logger;
    }

    public async Task<CreateAddressResult> Handle(CreateAddressCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // If this is the default address, clear existing defaults
            if (request.IsDefault)
            {
                await _addressRepository.ClearDefaultAsync(request.UserId, request.TenantId);
            }

            var address = new Address
            {
                UserId = request.UserId,
                TenantId = request.TenantId,
                Label = request.Label,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Phone = request.Phone,
                AddressLine1 = request.AddressLine1,
                AddressLine2 = request.AddressLine2,
                City = request.City,
                State = request.State,
                ZipCode = request.ZipCode,
                Country = request.Country,
                IsDefault = request.IsDefault
            };

            await _addressRepository.CreateAsync(address);

            _logger.LogInformation("Created address {AddressId} for user {UserId}", address.Id, request.UserId);

            return new CreateAddressResult(true, address.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating address for user {UserId}", request.UserId);
            return new CreateAddressResult(false, ErrorMessage: "Failed to create address");
        }
    }
}
