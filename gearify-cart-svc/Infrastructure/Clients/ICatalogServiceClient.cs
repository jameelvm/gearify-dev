using System.Threading;
using System.Threading.Tasks;
using Gearify.CartService.Infrastructure.Clients.DTOs;

namespace Gearify.CartService.Infrastructure.Clients;

public interface ICatalogServiceClient
{
    Task<ProductValidationResult?> GetProductAsync(string productId, CancellationToken cancellationToken = default);
}
