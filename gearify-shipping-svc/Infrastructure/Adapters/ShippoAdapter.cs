using Gearify.ShippingService.Domain;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gearify.ShippingService.Infrastructure.Adapters;

public class ShippoAdapter : IShippingAdapter
{
    public string ProviderName => "Shippo";

    public async Task<List<ShippingRate>> GetRatesAsync(CancellationToken ct)
    {
        await Task.Delay(100, ct);
        return new List<ShippingRate>
        {
            new() { Carrier = "UPS", ServiceLevel = "Ground", Amount = 18.75m, EstimatedDays = 4 }
        };
    }
}
