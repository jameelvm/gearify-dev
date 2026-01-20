using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.PaymentService.Infrastructure.Data;
using Gearify.PaymentService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Gearify.PaymentService.Infrastructure.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly PaymentDbContext _context;
    private readonly ILogger<UnitOfWork> _logger;
    private IDbContextTransaction? _transaction;
    private bool _committed;
    private bool _disposed;

    private IPaymentRepository? _paymentRepository;

    public UnitOfWork(PaymentDbContext context, ILogger<UnitOfWork> logger)
    {
        _context = context;
        _logger = logger;
    }

    public IPaymentRepository Payments => _paymentRepository ??= new PaymentRepository(_context);

    internal async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            _logger.LogWarning("Transaction already started");
            return;
        }

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        _logger.LogDebug("Database transaction started");
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Saved {Count} changes to database", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving changes to database");
            throw;
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_committed)
        {
            _logger.LogWarning("Transaction already committed");
            return;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);

            if (_transaction != null)
            {
                await _transaction.CommitAsync(cancellationToken);
                _logger.LogDebug("Transaction committed");
            }

            _committed = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error committing transaction");
            await RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            _logger.LogDebug("No transaction to rollback, clearing change tracker");
            _context.ChangeTracker.Clear();
            return;
        }

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
            _logger.LogDebug("Transaction rolled back");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rolling back transaction");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (_disposed) return;

        // Auto-rollback if not committed
        if (!_committed && _transaction != null)
        {
            _logger.LogWarning("UnitOfWork disposed without commit, rolling back transaction");
            try
            {
                await _transaction.RollbackAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during auto-rollback on dispose");
            }
        }

        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        await _context.DisposeAsync();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Auto-rollback if not committed (sync version)
            if (!_committed && _transaction != null)
            {
                _logger.LogWarning("UnitOfWork disposed without commit, rolling back transaction");
                try
                {
                    _transaction.Rollback();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during auto-rollback on dispose");
                }
            }

            _transaction?.Dispose();
            _context.Dispose();
        }

        _disposed = true;
    }
}
