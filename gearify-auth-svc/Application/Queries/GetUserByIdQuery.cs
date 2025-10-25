using Gearify.AuthService.Domain.Entities;
using MediatR;

namespace Gearify.AuthService.Application.Queries;

/// <summary>
/// Query to get a user by their ID
/// </summary>
public record GetUserByIdQuery(string UserId) : IRequest<User?>;
