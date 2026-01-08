using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.CatalogService.Application.DTOs;
using Gearify.CatalogService.Infrastructure.Configuration;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gearify.CatalogService.Application.Queries;

/// <summary>
/// Handler for getting sort options configuration
/// Returns tenant-specific sort options like Featured, Price Low-High, etc.
/// </summary>
public class GetSortOptionsQueryHandler : IRequestHandler<GetSortOptionsQuery, SortOptionsResponse>
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetSortOptionsQueryHandler> _logger;

    public GetSortOptionsQueryHandler(
        IAmazonDynamoDB dynamoDb,
        IOptions<CatalogDataSettings> catalogDataSettings,
        ITenantContext tenantContext,
        ILogger<GetSortOptionsQueryHandler> logger)
    {
        _dynamoDb = dynamoDb;
        _tableName = catalogDataSettings.Value.CatalogTableName;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<SortOptionsResponse> Handle(GetSortOptionsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;

        try
        {
            // Sort options are stored at tenant level (not department-specific)
            var getRequest = new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"TENANT#{tenantId}" } },
                    { "SK", new AttributeValue { S = "SORT_OPTIONS" } }
                }
            };

            var response = await _dynamoDb.GetItemAsync(getRequest, cancellationToken);

            if (!response.IsItemSet)
            {
                _logger.LogWarning("Sort options not found for tenant {TenantId}", tenantId);
                return new SortOptionsResponse(new List<SortOptionDto>());
            }

            var sortOptions = ParseSortOptions(response.Item);

            // Filter only active sort options and sort by display order
            var activeSortOptions = sortOptions
                .Where(so => so.IsActive)
                .OrderBy(so => so.DisplayOrder)
                .ToList();

            _logger.LogInformation("Retrieved {Count} sort options for tenant {TenantId}",
                activeSortOptions.Count, tenantId);

            return new SortOptionsResponse(activeSortOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sort options for tenant {TenantId}", tenantId);
            throw;
        }
    }

    private List<SortOptionDto> ParseSortOptions(Dictionary<string, AttributeValue> item)
    {
        var sortOptions = new List<SortOptionDto>();

        if (!item.TryGetValue("SortOptions", out var sortOptionsAttr) || sortOptionsAttr.L == null)
        {
            return sortOptions;
        }

        foreach (var optionAttr in sortOptionsAttr.L)
        {
            if (optionAttr.M == null) continue;

            var sortOption = new SortOptionDto
            {
                Id = optionAttr.M.TryGetValue("Id", out var id) ? id.S : string.Empty,
                Label = optionAttr.M.TryGetValue("Label", out var label) ? label.S : string.Empty,
                Value = optionAttr.M.TryGetValue("Value", out var value) ? value.S : string.Empty,
                SortType = optionAttr.M.TryGetValue("SortType", out var sortType) ? sortType.S : "Simple",
                FilterField = optionAttr.M.TryGetValue("FilterField", out var filterField) ? filterField.S : null,
                SortField = optionAttr.M.TryGetValue("SortField", out var sortField) ? sortField.S : string.Empty,
                SortDirection = optionAttr.M.TryGetValue("SortDirection", out var sortDirection) ? sortDirection.S : "ASC",
                DisplayOrder = optionAttr.M.TryGetValue("DisplayOrder", out var displayOrder) ? int.Parse(displayOrder.N) : 0,
                IsActive = optionAttr.M.TryGetValue("IsActive", out var isActive) && isActive.BOOL
            };

            sortOptions.Add(sortOption);
        }

        return sortOptions;
    }
}
