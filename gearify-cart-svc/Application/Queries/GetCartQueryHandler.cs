using System.Threading;
using System.Threading.Tasks;
using Gearify.CartService.Domain.Entities;
using Gearify.CartService.Infrastructure.Repositories;
using MediatR;

namespace Gearify.CartService.Application.Queries;

public class GetCartQueryHandler : IRequestHandler<GetCartQuery, Cart?>
{
    private readonly ICartRepository _repository;

    public GetCartQueryHandler(ICartRepository repository)
    {
        _repository = repository;
    }

    public async Task<Cart?> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        return await _repository.GetCartAsync(request.UserId, request.TenantId);
    }
}
