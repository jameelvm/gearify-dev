using Gearify.AuthService.Domain.Entities;
using MediatR;

namespace Gearify.AuthService.Application.Queries;

/// <summary>
/// Query to get a user by their email address
/// </summary>
public record GetUserByEmailQuery(string Email) : IRequest<User?>;
