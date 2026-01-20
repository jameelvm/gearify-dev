using System.Threading;
using System.Threading.Tasks;
using Gearify.PaymentService.Application.DTOs;
using Gearify.PaymentService.Application.Mappers;
using Gearify.PaymentService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.PaymentService.Application.Queries;

public class GetPaymentByIdQueryHandler : IRequestHandler<GetPaymentByIdQuery, PaymentDto?>
{
    private readonly IPaymentRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetPaymentByIdQueryHandler(
        IPaymentRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<PaymentDto?> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;

        var transaction = await _repository.GetTransactionByIdAsync(request.TransactionId);

        if (transaction == null || transaction.TenantId != tenantId)
        {
            return null;
        }

        return PaymentMapper.ToDto(transaction);
    }
}
