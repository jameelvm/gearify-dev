using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gearify.PaymentService.Application.DTOs;
using Gearify.PaymentService.Application.Mappers;
using Gearify.PaymentService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.PaymentService.Application.Queries;

public class GetPaymentsByUserQueryHandler : IRequestHandler<GetPaymentsByUserQuery, PaymentListDto>
{
    private readonly IPaymentRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetPaymentsByUserQueryHandler(
        IPaymentRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<PaymentListDto> Handle(GetPaymentsByUserQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;

        var (transactions, totalCount) = await _repository.GetTransactionsByUserAsync(
            tenantId,
            request.UserId,
            request.Page,
            request.PageSize
        );

        var payments = transactions.Select(PaymentMapper.ToSummaryDto).ToList();

        return new PaymentListDto
        {
            Payments = payments,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
        };
    }
}
