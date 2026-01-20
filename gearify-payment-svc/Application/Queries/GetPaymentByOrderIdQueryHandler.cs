using System.Threading;
using System.Threading.Tasks;
using Gearify.PaymentService.Application.DTOs;
using Gearify.PaymentService.Application.Mappers;
using Gearify.PaymentService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.PaymentService.Application.Queries;

public class GetPaymentByOrderIdQueryHandler : IRequestHandler<GetPaymentByOrderIdQuery, PaymentDto?>
{
    private readonly IPaymentRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetPaymentByOrderIdQueryHandler(
        IPaymentRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<PaymentDto?> Handle(GetPaymentByOrderIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;

        var transaction = await _repository.GetTransactionByOrderIdAsync(request.OrderId);

        if (transaction == null || transaction.TenantId != tenantId)
        {
            return null;
        }

        return PaymentMapper.ToDto(transaction);
    }
}
