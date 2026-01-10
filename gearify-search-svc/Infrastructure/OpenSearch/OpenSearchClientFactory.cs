using Elasticsearch.Net;
using Gearify.SearchService.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;

namespace Gearify.SearchService.Infrastructure.OpenSearch;

public interface IOpenSearchClientFactory
{
    IElasticClient CreateClient();
}

public class OpenSearchClientFactory : IOpenSearchClientFactory
{
    private readonly OpenSearchSettings _settings;
    private readonly ILogger<OpenSearchClientFactory> _logger;
    private IElasticClient? _client;

    public OpenSearchClientFactory(
        IOptions<OpenSearchSettings> settings,
        ILogger<OpenSearchClientFactory> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public IElasticClient CreateClient()
    {
        if (_client != null)
            return _client;

        var uri = new Uri(_settings.Endpoint);
        
        var connectionSettings = new ConnectionSettings(uri)
            .EnableDebugMode()
            .PrettyJson()
            .RequestTimeout(TimeSpan.FromSeconds(30))
            .OnRequestCompleted(callDetails =>
            {
                if (callDetails.RequestBodyInBytes != null)
                {
                    _logger.LogDebug("OpenSearch Request: {Request}",
                        System.Text.Encoding.UTF8.GetString(callDetails.RequestBodyInBytes));
                }
            });

        // Add basic auth if credentials provided
        if (!string.IsNullOrEmpty(_settings.Username) && !string.IsNullOrEmpty(_settings.Password))
        {
            connectionSettings.BasicAuthentication(_settings.Username, _settings.Password);
        }

        _client = new ElasticClient(connectionSettings);
        
        _logger.LogInformation("OpenSearch client created for endpoint: {Endpoint}", _settings.Endpoint);
        
        return _client;
    }
}
