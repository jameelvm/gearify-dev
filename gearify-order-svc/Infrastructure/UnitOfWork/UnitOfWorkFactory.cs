using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gearify.OrderService.Infrastructure.UnitOfWork;

public class UnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDbContextFactory<OrderDbContext> _dbContextFactory;
    private readonly ILoggerFactory _loggerFactory;

    public UnitOfWorkFactory(
        IServiceScopeFactory scopeFactory,
        IDbContextFactory<OrderDbContext> dbContextFactory,
        ILoggerFactory loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _dbContextFactory = dbContextFactory;
        _loggerFactory = loggerFactory;
    }

    public IUnitOfWork Create()
    {
        var context = _dbContextFactory.CreateDbContext();
        var logger = _loggerFactory.CreateLogger<UnitOfWork>();
        return new UnitOfWork(context, logger);
    }

    public async Task<IUnitOfWork> CreateWithTransactionAsync(CancellationToken cancellationToken = default)
    {
        var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var logger = _loggerFactory.CreateLogger<UnitOfWork>();
        var uow = new UnitOfWork(context, logger);
        await uow.BeginTransactionAsync(cancellationToken);
        return uow;
    }
}
