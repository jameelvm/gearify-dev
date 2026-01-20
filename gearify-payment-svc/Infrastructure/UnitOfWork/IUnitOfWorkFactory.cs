using System.Threading;
using System.Threading.Tasks;

namespace Gearify.PaymentService.Infrastructure.UnitOfWork;

public interface IUnitOfWorkFactory
{
    IUnitOfWork Create();
    Task<IUnitOfWork> CreateWithTransactionAsync(CancellationToken cancellationToken = default);
}
