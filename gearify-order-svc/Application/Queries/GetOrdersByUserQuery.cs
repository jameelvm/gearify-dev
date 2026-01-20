using System.Collections.Generic;
using Gearify.OrderService.Application.DTOs;
using MediatR;

namespace Gearify.OrderService.Application.Queries;

public record GetOrdersByUserQuery(string UserId) : IRequest<List<OrderSummaryDto>>;
