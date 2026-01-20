using Gearify.PaymentService.Application.DTOs;
using MediatR;

namespace Gearify.PaymentService.Application.Queries;

public record GetPaymentByOrderIdQuery(string OrderId) : IRequest<PaymentDto?>;
