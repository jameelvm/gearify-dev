namespace Gearify.SearchService.Infrastructure.Configuration;

public class OpenSearchSettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string IndexPrefix { get; set; } = "gearify-products";
}
