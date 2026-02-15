namespace Gearify.SharedKernel.Outbox;

public interface IOutboxWriter
{
    Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken ct = default);
}
