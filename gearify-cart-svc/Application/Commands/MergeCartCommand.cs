using System;
using Gearify.CartService.API.Models;
using MediatR;

namespace Gearify.CartService.Application.Commands;

public record MergeCartCommand(
    Guid GuestCartId,
    Guid UserId,
    MergeStrategy Strategy
) : IRequest<MergeCartResult>;

public record MergeCartResult(
    bool Success,
    CartResponse? Cart = null,
    string? ErrorMessage = null
);
