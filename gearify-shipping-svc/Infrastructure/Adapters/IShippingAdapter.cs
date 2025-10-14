using Gearify.ShippingService.Domain;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gearify.ShippingService.Infrastructure.Adapters;

public interface IShippingAdapter
{
    string ProviderName { get; }
    Task<List<ShippingRate>> GetRatesAsync(CancellationToken ct);
}
