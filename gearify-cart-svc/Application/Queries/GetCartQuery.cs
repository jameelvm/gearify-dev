using Gearify.CartService.API.Models;
using MediatR;

namespace Gearify.CartService.Application.Queries;

public record GetCartQuery(string UserId) : IRequest<CartResponse>;
