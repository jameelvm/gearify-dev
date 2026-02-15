using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.PaymentService.Infrastructure.Repositories;
using Gearify.SharedKernel.Outbox;

namespace Gearify.PaymentService.Infrastructure.UnitOfWork;

public interface IUnitOfWork : IAsyncDisposable, IDisposable, IOutboxWriter
{
    IPaymentRepository Payments { get; }

    /// <summary>
    /// Saves all changes and commits the transaction.
    /// Must be called before disposing to persist changes.
    /// If not called, Dispose will rollback any pending changes.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves changes without committing the transaction.
    /// Use this for intermediate saves within a transaction.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly rolls back all changes.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
