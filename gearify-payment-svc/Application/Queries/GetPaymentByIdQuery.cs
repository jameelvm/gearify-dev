using System;
using Gearify.PaymentService.Application.DTOs;
using MediatR;

namespace Gearify.PaymentService.Application.Queries;

public record GetPaymentByIdQuery(Guid TransactionId) : IRequest<PaymentDto?>;
