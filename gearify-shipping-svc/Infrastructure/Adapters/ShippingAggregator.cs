using Gearify.ShippingService.Domain;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gearify.ShippingService.Infrastructure.Adapters;

public class ShippingAggregator
{
    private readonly IEnumerable<IShippingAdapter> _adapters;

    public ShippingAggregator(IEnumerable<IShippingAdapter> adapters)
    {
        _adapters = adapters;
    }

    public async Task<List<ShippingRate>> GetAllRatesAsync(CancellationToken ct)
    {
        var tasks = _adapters.Select(adapter => adapter.GetRatesAsync(ct));
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r).OrderBy(r => r.Amount).ToList();
    }
}
