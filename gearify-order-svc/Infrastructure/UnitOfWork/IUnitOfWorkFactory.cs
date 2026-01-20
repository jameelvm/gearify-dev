using System.Threading;
using System.Threading.Tasks;

namespace Gearify.OrderService.Infrastructure.UnitOfWork;

public interface IUnitOfWorkFactory
{
    IUnitOfWork Create();
    Task<IUnitOfWork> CreateWithTransactionAsync(CancellationToken cancellationToken = default);
}
