using Gearify.AuthService.Application.Models;
using MediatR;

namespace Gearify.AuthService.Application.Queries;

/// <summary>
/// Query to get all active sessions for a user
/// </summary>
public record GetActiveSessionsQuery(string UserId) : IRequest<List<SessionInfo>>;
