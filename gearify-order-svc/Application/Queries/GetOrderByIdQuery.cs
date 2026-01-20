using System;
using Gearify.OrderService.Application.DTOs;
using MediatR;

namespace Gearify.OrderService.Application.Queries;

public record GetOrderByIdQuery(Guid OrderId) : IRequest<OrderDto?>;
