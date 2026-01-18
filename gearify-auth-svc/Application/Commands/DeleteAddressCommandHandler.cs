using Gearify.AuthService.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Handler for DeleteAddressCommand
/// </summary>
public class DeleteAddressCommandHandler : IRequestHandler<DeleteAddressCommand, DeleteAddressResult>
{
    private readonly IAddressRepository _addressRepository;
    private readonly ILogger<DeleteAddressCommandHandler> _logger;

    public DeleteAddressCommandHandler(
        IAddressRepository addressRepository,
        ILogger<DeleteAddressCommandHandler> logger)
    {
        _addressRepository = addressRepository;
        _logger = logger;
    }

    public async Task<DeleteAddressResult> Handle(DeleteAddressCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var address = await _addressRepository.GetByIdAsync(request.AddressId, request.UserId, request.TenantId);

            if (address == null)
            {
                return new DeleteAddressResult(false, "Address not found");
            }

            await _addressRepository.DeleteAsync(request.AddressId, request.UserId, request.TenantId);

            _logger.LogInformation("Deleted address {AddressId} for user {UserId}", request.AddressId, request.UserId);

            return new DeleteAddressResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting address {AddressId}", request.AddressId);
            return new DeleteAddressResult(false, "Failed to delete address");
        }
    }
}
