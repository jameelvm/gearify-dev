using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;

namespace Gearify.TenantService.Domain;

[DynamoDBTable("gearify-tenants")]
public class Tenant
{
    [DynamoDBHashKey]
    public string TenantId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Dictionary<string, bool> FeatureFlags { get; set; } = new();
}
