namespace Gearify.ShippingService.Domain;

public class ShippingRate
{
    public string Carrier { get; set; } = string.Empty;
    public string ServiceLevel { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public int EstimatedDays { get; set; }
}

public class Address
{
    public string Street1 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class CustomsInfo
{
    public string HsCode { get; set; } = string.Empty;
    public decimal CustomsValue { get; set; }
    public string Incoterm { get; set; } = "DDU";
}
