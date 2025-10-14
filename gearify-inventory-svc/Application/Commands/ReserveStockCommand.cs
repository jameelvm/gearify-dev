using Amazon.DynamoDBv2.DataModel;
using Gearify.InventoryService.Domain;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Gearify.InventoryService.Application.Commands;

public record ReserveStockCommand(string ProductId, int Quantity, string OrderId) : IRequest<Result>;

public record Result(bool Success, string? ReservationId, string? Error);

public class ReserveStockHandler : IRequestHandler<ReserveStockCommand, Result>
{
    private readonly IDynamoDBContext _context;

    public ReserveStockHandler(IDynamoDBContext context) => _context = context;

    public async Task<Result> Handle(ReserveStockCommand cmd, CancellationToken ct)
    {
        var inventory = await _context.LoadAsync<InventoryItem>(cmd.ProductId, ct);

        if (inventory == null || inventory.AvailableQuantity < cmd.Quantity)
        {
            return new Result(false, null, "Insufficient stock");
        }

        inventory.AvailableQuantity -= cmd.Quantity;
        inventory.ReservedQuantity += cmd.Quantity;
        await _context.SaveAsync(inventory, ct);

        var reservation = new StockReservation
        {
            ProductId = cmd.ProductId,
            OrderId = cmd.OrderId,
            Quantity = cmd.Quantity
        };

        await _context.SaveAsync(reservation, ct);

        return new Result(true, reservation.ReservationId, null);
    }
}
