using Gearify.ShippingService.Domain;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gearify.ShippingService.Infrastructure.Adapters;

public class EasyPostAdapter : IShippingAdapter
{
    public string ProviderName => "EasyPost";

    public async Task<List<ShippingRate>> GetRatesAsync(CancellationToken ct)
    {
        await Task.Delay(100, ct);
        return new List<ShippingRate>
        {
            new() { Carrier = "USPS", ServiceLevel = "Priority", Amount = 15.99m, EstimatedDays = 3 },
            new() { Carrier = "FedEx", ServiceLevel = "Ground", Amount = 22.50m, EstimatedDays = 5 }
        };
    }
}
