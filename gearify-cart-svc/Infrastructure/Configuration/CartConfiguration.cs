namespace Gearify.CartService.Infrastructure.Configuration;

public class CartConfiguration
{
    public const string SectionName = "CartConfiguration";

    public int GuestCartExpirationDays { get; set; } = 7;
    public int DefaultExpirationDays { get; set; } = 30;
}
