using Gearify.AuthService.Domain.Entities;
using Gearify.AuthService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Queries;

/// <summary>
/// Handles query to get user by ID
/// </summary>
public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, User?>
{
    private readonly IUserRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetUserByIdQueryHandler> _logger;

    public GetUserByIdQueryHandler(
        IUserRepository repository,
        ITenantContext tenantContext,
        ILogger<GetUserByIdQueryHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<User?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            var user = await _repository.GetByIdAsync(request.UserId, tenantId);

            if (user != null)
            {
                _logger.LogInformation("User {UserId} retrieved successfully", request.UserId);
            }

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", request.UserId);
            throw;
        }
    }
}
