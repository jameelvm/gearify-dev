using Gearify.OrderService.Application.DTOs;
using MediatR;

namespace Gearify.OrderService.Application.Queries;

public record GetOrderByNumberQuery(string OrderNumber) : IRequest<OrderDto?>;
