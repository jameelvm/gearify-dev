using Gearify.CartService.Domain.Entities;
using MediatR;

namespace Gearify.CartService.Application.Queries;

public record GetCartQuery(string UserId) : IRequest<Cart?>;
