using Gearify.AuthService.Domain.Entities;
using Gearify.AuthService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Queries;

/// <summary>
/// Handles query to get user by email
/// </summary>
public class GetUserByEmailQueryHandler : IRequestHandler<GetUserByEmailQuery, User?>
{
    private readonly IUserRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetUserByEmailQueryHandler> _logger;

    public GetUserByEmailQueryHandler(
        IUserRepository repository,
        ITenantContext tenantContext,
        ILogger<GetUserByEmailQueryHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<User?> Handle(GetUserByEmailQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            var user = await _repository.GetByEmailAsync(request.Email, tenantId);

            if (user != null)
            {
                _logger.LogInformation("User with email {Email} retrieved successfully", request.Email);
            }

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user by email {Email}", request.Email);
            throw;
        }
    }
}
