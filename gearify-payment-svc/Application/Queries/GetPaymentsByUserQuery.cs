using Gearify.PaymentService.Application.DTOs;
using MediatR;

namespace Gearify.PaymentService.Application.Queries;

public record GetPaymentsByUserQuery(
    string UserId,
    int Page = 1,
    int PageSize = 20
) : IRequest<PaymentListDto>;
