# Featured Products Implementation Guide

Complete guide for implementing flexible featured products functionality in Gearify e-commerce platform.

## Overview

This feature allows administrators to manually curate featured products that appear in various sections of the website (homepage, category pages, deals page, etc.) with full control over:

- Which products are featured
- Where they appear (sections)
- Display priority/ordering
- Time-based scheduling (featured from/to dates)
- Multiple placements per product

## Business Requirements

### User Stories

**As an Admin:**
- I want to mark products as featured for specific sections
- I want to set display order for featured products
- I want to schedule featured products (start and end dates)
- I want to feature the same product in multiple sections
- I want to automatically hide expired featured products

**As a Customer:**
- I want to see featured products prominently on the homepage
- I want to see category-specific featured products
- I want to see fresh/rotating featured products

### Success Metrics
- Increase homepage conversion rate by 15%
- Increase click-through rate on featured products to 8%+
- Reduce bounce rate by 10%

---

## Database Schema Changes

### Product Entity Updates

```csharp
// File: Gearify.CatalogService/Domain/Entities/Product.cs

public class Product
{
    // ... existing fields ...

    // Featured Product Fields

    /// <summary>
    /// Indicates if this product is manually featured
    /// </summary>
    public bool IsFeatured { get; set; }

    /// <summary>
    /// Display priority for featured products (1 = highest priority, 0 = not featured)
    /// Lower numbers appear first
    /// </summary>
    public int FeaturedPriority { get; set; }

    /// <summary>
    /// List of sections where this product should be featured
    /// Examples: "homepage", "cricket", "deals", "new-arrivals", "best-sellers"
    /// </summary>
    public List<string> FeaturedSections { get; set; } = new();

    /// <summary>
    /// When this product should start being featured (inclusive)
    /// Null = immediate
    /// </summary>
    public DateTime? FeaturedFrom { get; set; }

    /// <summary>
    /// When this product should stop being featured (inclusive)
    /// Null = no expiry
    /// </summary>
    public DateTime? FeaturedUntil { get; set; }

    /// <summary>
    /// Auto-calculated trending score (updated by background job)
    /// Used as secondary sort when FeaturedPriority is the same
    /// Score = (ViewCount * 0.3) + (SalesCount * 0.5) + (RatingAverage * 10) + (Recency * 0.2)
    /// </summary>
    public decimal TrendingScore { get; set; }

    // Helper method
    public bool IsFeaturedNow(string section)
    {
        if (!IsFeatured || !FeaturedSections.Contains(section))
            return false;

        var now = DateTime.UtcNow;

        if (FeaturedFrom.HasValue && now < FeaturedFrom.Value)
            return false;

        if (FeaturedUntil.HasValue && now > FeaturedUntil.Value)
            return false;

        return true;
    }
}
```

### DynamoDB Item Structure

```json
{
  "PK": { "S": "TENANT#default" },
  "SK": { "S": "PRODUCT#prod-bat-ss-001" },
  "GSI1PK": { "S": "TENANT#default#PRODUCTS" },
  "GSI1SK": { "S": "PRODUCT#prod-bat-ss-001" },
  "Id": { "S": "prod-bat-ss-001" },
  "TenantId": { "S": "default" },
  "Name": { "S": "SS Ton Reserve Edition English Willow Cricket Bat" },
  "Price": { "N": "12500" },

  // Featured fields
  "IsFeatured": { "BOOL": true },
  "FeaturedPriority": { "N": "1" },
  "FeaturedSections": { "SS": ["homepage", "cricket", "deals"] },
  "FeaturedFrom": { "S": "2026-01-01T00:00:00.000Z" },
  "FeaturedUntil": { "S": "2026-01-31T23:59:59.000Z" },
  "TrendingScore": { "N": "85.5" }
}
```

### Migration Script

Update existing products to add featured fields:

```bash
# File: gearify-umbrella/localstack/scripts/add-featured-fields.sh

#!/bin/bash

# Add featured fields to existing products
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test aws dynamodb scan \
  --table-name gearify-products \
  --endpoint-url http://localhost:4566 \
  --region us-east-1 \
  --output json | \
jq -r '.Items[] | select(.PK.S | startswith("TENANT#")) | .PK.S + " " + .SK.S' | \
while read pk sk; do
  echo "Updating $pk $sk"

  AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test aws dynamodb update-item \
    --table-name gearify-products \
    --endpoint-url http://localhost:4566 \
    --region us-east-1 \
    --key "{\"PK\": {\"S\": \"$pk\"}, \"SK\": {\"S\": \"$sk\"}}" \
    --update-expression "SET IsFeatured = :false, FeaturedPriority = :zero, FeaturedSections = :empty, TrendingScore = :zero" \
    --expression-attribute-values '{
      ":false": {"BOOL": false},
      ":zero": {"N": "0"},
      ":empty": {"SS": ["none"]}
    }' \
    --return-values UPDATED_NEW
done
```

---

## Backend Implementation

### 1. Query: Get Featured Products

```csharp
// File: Gearify.CatalogService/Application/Queries/GetFeaturedProductsQuery.cs

using Gearify.CatalogService.Application.DTOs;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

/// <summary>
/// Query to retrieve featured products for a specific section
/// </summary>
public record GetFeaturedProductsQuery(
    string? Section = "homepage",
    int Limit = 10
) : IRequest<ProductListResponse>;
```

```csharp
// File: Gearify.CatalogService/Application/Queries/GetFeaturedProductsQueryHandler.cs

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.CatalogService.Application.DTOs;
using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Configuration;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gearify.CatalogService.Application.Queries;

public class GetFeaturedProductsQueryHandler : IRequestHandler<GetFeaturedProductsQuery, ProductListResponse>
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetFeaturedProductsQueryHandler> _logger;

    public GetFeaturedProductsQueryHandler(
        IAmazonDynamoDB dynamoDb,
        IOptions<CatalogDataSettings> catalogDataSettings,
        ITenantContext tenantContext,
        ILogger<GetFeaturedProductsQueryHandler> logger)
    {
        _dynamoDb = dynamoDb;
        _tableName = catalogDataSettings.Value.ProductsTableName;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ProductListResponse> Handle(GetFeaturedProductsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var now = DateTime.UtcNow;
        var section = request.Section ?? "homepage";

        var queryRequest = new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :gsi1pk",
            FilterExpression = "IsFeatured = :featured AND contains(FeaturedSections, :section)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":gsi1pk", new AttributeValue { S = $"TENANT#{tenantId}#PRODUCTS" } },
                { ":featured", new AttributeValue { BOOL = true } },
                { ":section", new AttributeValue { S = section } }
            }
        };

        // Add date filters if needed
        // Note: DynamoDB FilterExpression doesn't support OR with attribute_not_exists easily
        // So we'll filter in-memory after retrieval

        try
        {
            var response = await _dynamoDb.QueryAsync(queryRequest, cancellationToken);

            var products = response.Items
                .Select(MapToProduct)
                .Where(p => IsFeaturedNow(p, section, now)) // Filter by date
                .OrderBy(p => p.FeaturedPriority)
                .ThenByDescending(p => p.TrendingScore)
                .Take(request.Limit)
                .ToList();

            var productDtos = products.Select(ProductListDto.FromProduct).ToList();

            _logger.LogInformation(
                "Retrieved {Count} featured products for tenant {TenantId} in section {Section}",
                products.Count, tenantId, section);

            return new ProductListResponse(productDtos, productDtos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving featured products for tenant {TenantId} in section {Section}", tenantId, section);
            throw;
        }
    }

    private bool IsFeaturedNow(Product product, string section, DateTime now)
    {
        if (!product.IsFeatured || !product.FeaturedSections.Contains(section))
            return false;

        if (product.FeaturedFrom.HasValue && now < product.FeaturedFrom.Value)
            return false;

        if (product.FeaturedUntil.HasValue && now > product.FeaturedUntil.Value)
            return false;

        return true;
    }

    private Product MapToProduct(Dictionary<string, AttributeValue> item)
    {
        return new Product
        {
            Id = item["Id"].S,
            TenantId = item["TenantId"].S,
            Name = item["Name"].S,
            Description = item.TryGetValue("Description", out var desc) ? desc.S : string.Empty,
            Sku = item.TryGetValue("Sku", out var sku) ? sku.S : string.Empty,
            Price = item.TryGetValue("Price", out var price) ? decimal.Parse(price.N) : 0,
            CompareAtPrice = item.TryGetValue("CompareAtPrice", out var comparePrice) ? decimal.Parse(comparePrice.N) : 0,
            Currency = item.TryGetValue("Currency", out var currency) ? currency.S : "USD",
            DiscountPercentage = item.TryGetValue("DiscountPercentage", out var discountPct) ? decimal.Parse(discountPct.N) : null,
            OfferBadge = item.TryGetValue("OfferBadge", out var offerBadge) ? offerBadge.S : null,
            RatingAverage = item.TryGetValue("RatingAverage", out var ratingAvg) ? decimal.Parse(ratingAvg.N) : null,
            RatingCount = item.TryGetValue("RatingCount", out var ratingCnt) ? int.Parse(ratingCnt.N) : null,
            Department = item.TryGetValue("Department", out var dept) ? dept.S : string.Empty,
            DepartmentSlug = item.TryGetValue("DepartmentSlug", out var deptSlug) ? deptSlug.S : string.Empty,
            Category = item.TryGetValue("Category", out var cat) ? cat.S : string.Empty,
            CategorySlug = item.TryGetValue("CategorySlug", out var catSlug) ? catSlug.S : string.Empty,
            Subcategory = item.TryGetValue("Subcategory", out var subcat) ? subcat.S : string.Empty,
            SubcategorySlug = item.TryGetValue("SubcategorySlug", out var subcatSlug) ? subcatSlug.S : string.Empty,
            Brand = item.TryGetValue("Brand", out var brand) ? brand.S : string.Empty,
            BrandSlug = item.TryGetValue("BrandSlug", out var brandSlug) ? brandSlug.S : string.Empty,
            ThumbnailUrl = item.TryGetValue("ThumbnailUrl", out var thumbnailUrl) ? thumbnailUrl.S : null,
            ImageUrls = item.TryGetValue("ImageUrls", out var images) ? images.SS : new List<string>(),
            Tags = item.TryGetValue("Tags", out var tags) ? tags.SS : new List<string>(),
            IsActive = item.TryGetValue("IsActive", out var active) && active.BOOL,

            // Featured fields
            IsFeatured = item.TryGetValue("IsFeatured", out var featured) && featured.BOOL,
            FeaturedPriority = item.TryGetValue("FeaturedPriority", out var featPriority) ? int.Parse(featPriority.N) : 0,
            FeaturedSections = item.TryGetValue("FeaturedSections", out var featSections) ? featSections.SS : new List<string>(),
            FeaturedFrom = item.TryGetValue("FeaturedFrom", out var featFrom) ? DateTime.Parse(featFrom.S) : null,
            FeaturedUntil = item.TryGetValue("FeaturedUntil", out var featUntil) ? DateTime.Parse(featUntil.S) : null,
            TrendingScore = item.TryGetValue("TrendingScore", out var trending) ? decimal.Parse(trending.N) : 0,

            CreatedAt = item.TryGetValue("CreatedAt", out var created) ? DateTime.Parse(created.S) : DateTime.UtcNow,
            UpdatedAt = item.TryGetValue("UpdatedAt", out var updated) ? DateTime.Parse(updated.S) : DateTime.UtcNow,
            CreatedBy = item.TryGetValue("CreatedBy", out var createdBy) ? createdBy.S : string.Empty,
            UpdatedBy = item.TryGetValue("UpdatedBy", out var updatedBy) ? updatedBy.S : string.Empty
        };
    }
}
```

### 2. Command: Update Featured Status

```csharp
// File: Gearify.CatalogService/Application/Commands/UpdateProductFeaturedStatusCommand.cs

using Gearify.CatalogService.Application.DTOs;
using MediatR;

namespace Gearify.CatalogService.Application.Commands;

/// <summary>
/// Command to update a product's featured status
/// </summary>
public record UpdateProductFeaturedStatusCommand(
    string ProductId,
    bool IsFeatured,
    int FeaturedPriority,
    List<string> FeaturedSections,
    DateTime? FeaturedFrom,
    DateTime? FeaturedUntil
) : IRequest<CommandResult>;
```

```csharp
// File: Gearify.CatalogService/Application/Commands/UpdateProductFeaturedStatusCommandHandler.cs

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.CatalogService.Application.DTOs;
using Gearify.CatalogService.Infrastructure.Configuration;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gearify.CatalogService.Application.Commands;

public class UpdateProductFeaturedStatusCommandHandler : IRequestHandler<UpdateProductFeaturedStatusCommand, CommandResult>
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UpdateProductFeaturedStatusCommandHandler> _logger;

    public UpdateProductFeaturedStatusCommandHandler(
        IAmazonDynamoDB dynamoDb,
        IOptions<CatalogDataSettings> catalogDataSettings,
        ITenantContext tenantContext,
        ILogger<UpdateProductFeaturedStatusCommandHandler> logger)
    {
        _dynamoDb = dynamoDb;
        _tableName = catalogDataSettings.Value.ProductsTableName;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<CommandResult> Handle(UpdateProductFeaturedStatusCommand command, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;

        try
        {
            var updateRequest = new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"TENANT#{tenantId}" } },
                    { "SK", new AttributeValue { S = $"PRODUCT#{command.ProductId}" } }
                },
                UpdateExpression = "SET IsFeatured = :featured, FeaturedPriority = :priority, FeaturedSections = :sections, UpdatedAt = :now",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":featured", new AttributeValue { BOOL = command.IsFeatured } },
                    { ":priority", new AttributeValue { N = command.FeaturedPriority.ToString() } },
                    { ":sections", new AttributeValue { SS = command.FeaturedSections.Any() ? command.FeaturedSections : new List<string> { "none" } } },
                    { ":now", new AttributeValue { S = DateTime.UtcNow.ToString("o") } }
                }
            };

            // Add optional date fields
            if (command.FeaturedFrom.HasValue)
            {
                updateRequest.UpdateExpression += ", FeaturedFrom = :from";
                updateRequest.ExpressionAttributeValues.Add(":from", new AttributeValue { S = command.FeaturedFrom.Value.ToString("o") });
            }
            else
            {
                updateRequest.UpdateExpression += " REMOVE FeaturedFrom";
            }

            if (command.FeaturedUntil.HasValue)
            {
                updateRequest.UpdateExpression += ", FeaturedUntil = :until";
                updateRequest.ExpressionAttributeValues.Add(":until", new AttributeValue { S = command.FeaturedUntil.Value.ToString("o") });
            }
            else
            {
                updateRequest.UpdateExpression += " REMOVE FeaturedUntil";
            }

            await _dynamoDb.UpdateItemAsync(updateRequest, cancellationToken);

            _logger.LogInformation(
                "Updated featured status for product {ProductId}: Featured={IsFeatured}, Priority={Priority}, Sections={Sections}",
                command.ProductId, command.IsFeatured, command.FeaturedPriority, string.Join(",", command.FeaturedSections));

            return new CommandResult
            {
                Success = true,
                Message = "Product featured status updated successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating featured status for product {ProductId}", command.ProductId);
            return new CommandResult
            {
                Success = false,
                ErrorMessage = "Failed to update product featured status"
            };
        }
    }
}
```

### 3. API Controller Endpoints

```csharp
// File: Gearify.CatalogService/API/Controllers/ProductsController.cs

// Add these endpoints to existing ProductsController

/// <summary>
/// Get featured products for a specific section
/// </summary>
[HttpGet("featured")]
[ProducesResponseType(typeof(ProductListResponse), 200)]
[ProducesResponseType(400)]
public async Task<IActionResult> GetFeaturedProducts(
    [FromQuery] string? section = "homepage",
    [FromQuery] int limit = 10)
{
    try
    {
        var result = await _mediator.Send(new GetFeaturedProductsQuery(section, limit));
        return Ok(result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving featured products for section {Section}", section);
        return StatusCode(500, new { error = "Internal server error" });
    }
}

/// <summary>
/// Update a product's featured status (Admin only)
/// </summary>
[HttpPut("products/{id}/featured")]
[ProducesResponseType(200)]
[ProducesResponseType(400)]
[ProducesResponseType(404)]
// [Authorize(Roles = "Admin")] // Uncomment when auth is ready
public async Task<IActionResult> UpdateProductFeaturedStatus(
    string id,
    [FromBody] UpdateFeaturedStatusRequest request)
{
    try
    {
        var command = new UpdateProductFeaturedStatusCommand(
            ProductId: id,
            IsFeatured: request.IsFeatured,
            FeaturedPriority: request.FeaturedPriority,
            FeaturedSections: request.FeaturedSections ?? new List<string>(),
            FeaturedFrom: request.FeaturedFrom,
            FeaturedUntil: request.FeaturedUntil
        );

        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { message = result.Message });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error updating featured status for product {ProductId}", id);
        return StatusCode(500, new { error = "Internal server error" });
    }
}

/// <summary>
/// DTO for updating featured status
/// </summary>
public record UpdateFeaturedStatusRequest(
    bool IsFeatured,
    int FeaturedPriority,
    List<string>? FeaturedSections,
    DateTime? FeaturedFrom,
    DateTime? FeaturedUntil
);
```

---

## Frontend Implementation

### 1. Angular Service

```typescript
// File: gearify-web/src/app/core/services/featured-products.service.ts

import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ProductListResponse } from '@core/models/product.model';
import { API_CONFIG } from '@shared/constants/api.constants';

export interface UpdateFeaturedStatusRequest {
  isFeatured: boolean;
  featuredPriority: number;
  featuredSections: string[];
  featuredFrom?: string;
  featuredUntil?: string;
}

@Injectable({
  providedIn: 'root'
})
export class FeaturedProductsService {
  private http = inject(HttpClient);

  /**
   * Get featured products for a specific section
   */
  getFeaturedProducts(
    section: string = 'homepage',
    limit: number = 10
  ): Observable<ProductListResponse> {
    const params = new HttpParams()
      .set('section', section)
      .set('limit', limit.toString());

    return this.http.get<ProductListResponse>(
      `${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.PRODUCTS}/featured`,
      { params }
    );
  }

  /**
   * Update a product's featured status (Admin only)
   */
  updateFeaturedStatus(
    productId: string,
    request: UpdateFeaturedStatusRequest
  ): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(
      `${API_CONFIG.BASE_URL}${API_CONFIG.ENDPOINTS.PRODUCTS}/products/${productId}/featured`,
      request
    );
  }
}
```

### 2. Homepage Component Integration

```typescript
// File: gearify-web/src/app/features/home/home.component.ts

import { Component, OnInit, signal, inject } from '@angular/core';
import { FeaturedProductsService } from '@core/services/featured-products.service';
import { Product } from '@core/models/product.model';

@Component({
  selector: 'app-home',
  // ... imports
})
export class HomeComponent implements OnInit {
  private featuredService = inject(FeaturedProductsService);

  // Featured products signals
  homepageFeatured = signal<Product[]>([]);
  dealsFeatured = signal<Product[]>([]);
  newArrivalsFeatured = signal<Product[]>([]);

  ngOnInit(): void {
    this.loadFeaturedProducts();
  }

  private loadFeaturedProducts(): void {
    // Homepage featured products
    this.featuredService.getFeaturedProducts('homepage', 8).subscribe({
      next: (response) => {
        this.homepageFeatured.set(response.products);
      },
      error: (err) => {
        console.error('Error loading homepage featured products:', err);
      }
    });

    // Deals section
    this.featuredService.getFeaturedProducts('deals', 4).subscribe({
      next: (response) => {
        this.dealsFeatured.set(response.products);
      },
      error: (err) => {
        console.error('Error loading deals featured products:', err);
      }
    });

    // New arrivals
    this.featuredService.getFeaturedProducts('new-arrivals', 6).subscribe({
      next: (response) => {
        this.newArrivalsFeatured.set(response.products);
      },
      error: (err) => {
        console.error('Error loading new arrivals:', err);
      }
    });
  }
}
```

```html
<!-- File: gearify-web/src/app/features/home/home.component.html -->

<!-- Featured Products Section -->
<section class="featured-products">
  <div class="container">
    <div class="section-header">
      <h2>Featured Products</h2>
      <a routerLink="/products" class="view-all">View All</a>
    </div>
    <div class="products-grid">
      @for (product of homepageFeatured(); track product.id) {
        <app-product-card
          [product]="product"
          [showFeaturedBadge]="true"
          (productClicked)="handleProductClick(product)"
          (addToCart)="handleAddToCart(product)">
        </app-product-card>
      }
    </div>
  </div>
</section>

<!-- Hot Deals Section -->
<section class="deals-section">
  <div class="container">
    <div class="section-header">
      <h2>🔥 Hot Deals</h2>
    </div>
    <div class="products-grid">
      @for (product of dealsFeatured(); track product.id) {
        <app-product-card
          [product]="product"
          (productClicked)="handleProductClick(product)"
          (addToCart)="handleAddToCart(product)">
        </app-product-card>
      }
    </div>
  </div>
</section>
```

### 3. Admin Featured Products Manager

```typescript
// File: gearify-web/src/app/admin/components/featured-products-manager.component.ts

import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProductService } from '@core/services/product.service';
import { FeaturedProductsService, UpdateFeaturedStatusRequest } from '@core/services/featured-products.service';
import { Product } from '@core/models/product.model';

interface FeaturedProductForm {
  productId: string;
  isFeatured: boolean;
  featuredPriority: number;
  featuredSections: string[];
  featuredFrom?: Date;
  featuredUntil?: Date;
}

@Component({
  selector: 'app-featured-products-manager',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="featured-manager">
      <h1>Featured Products Manager</h1>

      <!-- Search Products -->
      <div class="search-section">
        <input
          type="text"
          [(ngModel)]="searchQuery"
          (input)="searchProducts()"
          placeholder="Search products..."
          class="search-input">
      </div>

      <!-- Product List -->
      <div class="product-list">
        @for (product of products(); track product.id) {
          <div class="product-row">
            <div class="product-info">
              <img [src]="product.thumbnailUrl || 'placeholder.png'" alt="{{ product.name }}">
              <div>
                <h4>{{ product.name }}</h4>
                <p>{{ product.category }} - ₹{{ product.price }}</p>
              </div>
            </div>

            <div class="featured-controls">
              <label>
                <input
                  type="checkbox"
                  [(ngModel)]="featuredForms[product.id].isFeatured"
                  (change)="onFeaturedToggle(product.id)">
                Featured
              </label>

              @if (featuredForms[product.id].isFeatured) {
                <div class="featured-options">
                  <label>
                    Priority:
                    <input
                      type="number"
                      [(ngModel)]="featuredForms[product.id].featuredPriority"
                      min="1"
                      max="100">
                  </label>

                  <label>
                    Sections:
                    <select
                      multiple
                      [(ngModel)]="featuredForms[product.id].featuredSections"
                      class="sections-select">
                      <option value="homepage">Homepage</option>
                      <option value="cricket">Cricket</option>
                      <option value="deals">Deals</option>
                      <option value="new-arrivals">New Arrivals</option>
                      <option value="best-sellers">Best Sellers</option>
                    </select>
                  </label>

                  <label>
                    Featured From:
                    <input
                      type="datetime-local"
                      [(ngModel)]="featuredForms[product.id].featuredFrom">
                  </label>

                  <label>
                    Featured Until:
                    <input
                      type="datetime-local"
                      [(ngModel)]="featuredForms[product.id].featuredUntil">
                  </label>

                  <button
                    (click)="saveFeaturedStatus(product.id)"
                    class="btn-save">
                    Save
                  </button>
                </div>
              }
            </div>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .featured-manager {
      padding: 20px;
    }

    .search-input {
      width: 100%;
      padding: 10px;
      margin-bottom: 20px;
      border: 1px solid #ddd;
      border-radius: 4px;
    }

    .product-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 15px;
      border: 1px solid #ddd;
      margin-bottom: 10px;
      border-radius: 4px;
    }

    .product-info {
      display: flex;
      gap: 15px;
      align-items: center;
    }

    .product-info img {
      width: 60px;
      height: 60px;
      object-fit: cover;
      border-radius: 4px;
    }

    .featured-controls {
      display: flex;
      flex-direction: column;
      gap: 10px;
    }

    .featured-options {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 10px;
      padding: 10px;
      background: #f5f5f5;
      border-radius: 4px;
    }

    .sections-select {
      height: 80px;
    }

    .btn-save {
      padding: 8px 16px;
      background: #4CAF50;
      color: white;
      border: none;
      border-radius: 4px;
      cursor: pointer;
    }
  `]
})
export class FeaturedProductsManagerComponent implements OnInit {
  private productService = inject(ProductService);
  private featuredService = inject(FeaturedProductsService);

  products = signal<Product[]>([]);
  searchQuery = '';
  featuredForms: Record<string, FeaturedProductForm> = {};

  ngOnInit(): void {
    this.loadProducts();
  }

  loadProducts(): void {
    this.productService.getProductsBySlug({}).subscribe({
      next: (response) => {
        this.products.set(response.products);
        this.initializeForms(response.products);
      },
      error: (err) => {
        console.error('Error loading products:', err);
      }
    });
  }

  initializeForms(products: Product[]): void {
    products.forEach(product => {
      this.featuredForms[product.id] = {
        productId: product.id,
        isFeatured: product.isFeatured || false,
        featuredPriority: product.featuredPriority || 10,
        featuredSections: product.featuredSections || [],
        featuredFrom: product.featuredFrom ? new Date(product.featuredFrom) : undefined,
        featuredUntil: product.featuredUntil ? new Date(product.featuredUntil) : undefined
      };
    });
  }

  onFeaturedToggle(productId: string): void {
    if (!this.featuredForms[productId].isFeatured) {
      // Reset when unfeaturing
      this.featuredForms[productId].featuredSections = [];
      this.featuredForms[productId].featuredPriority = 10;
    }
  }

  saveFeaturedStatus(productId: string): void {
    const form = this.featuredForms[productId];

    const request: UpdateFeaturedStatusRequest = {
      isFeatured: form.isFeatured,
      featuredPriority: form.featuredPriority,
      featuredSections: form.featuredSections,
      featuredFrom: form.featuredFrom?.toISOString(),
      featuredUntil: form.featuredUntil?.toISOString()
    };

    this.featuredService.updateFeaturedStatus(productId, request).subscribe({
      next: (response) => {
        alert(response.message);
      },
      error: (err) => {
        console.error('Error updating featured status:', err);
        alert('Failed to update featured status');
      }
    });
  }

  searchProducts(): void {
    // Implement search/filter logic
  }
}
```

---

## Testing

### 1. Backend Unit Tests

```csharp
// File: Gearify.CatalogService.Tests/Queries/GetFeaturedProductsQueryHandlerTests.cs

using Xunit;
using Moq;
using Amazon.DynamoDBv2;
using Gearify.CatalogService.Application.Queries;

public class GetFeaturedProductsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFeaturedProducts_ForHomepageSection()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        // Setup mock to return test data

        var handler = new GetFeaturedProductsQueryHandler(
            mockDynamoDb.Object,
            /* other dependencies */
        );

        var query = new GetFeaturedProductsQuery("homepage", 10);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Total <= 10);
    }

    [Fact]
    public async Task Handle_FiltersExpiredFeaturedProducts()
    {
        // Test that expired featured products are not returned
    }

    [Fact]
    public async Task Handle_OrdersByPriorityThenTrendingScore()
    {
        // Test ordering logic
    }
}
```

### 2. API Integration Tests

```bash
# Test Get Featured Products
curl -X GET "http://localhost:8080/api/catalog/products/featured?section=homepage&limit=5"

# Expected Response:
{
  "products": [
    {
      "id": "prod-bat-ss-001",
      "name": "SS Ton Reserve Edition",
      "price": 12500,
      "isFeatured": true,
      "featuredPriority": 1,
      "featuredSections": ["homepage", "cricket"]
    }
  ],
  "total": 5
}

# Test Update Featured Status
curl -X PUT "http://localhost:8080/api/catalog/products/prod-bat-ss-001/featured" \
  -H "Content-Type: application/json" \
  -d '{
    "isFeatured": true,
    "featuredPriority": 1,
    "featuredSections": ["homepage", "cricket", "deals"],
    "featuredFrom": "2026-01-01T00:00:00Z",
    "featuredUntil": "2026-01-31T23:59:59Z"
  }'

# Expected Response:
{
  "message": "Product featured status updated successfully"
}
```

---

## Sample Data

### Mark Products as Featured

```bash
# File: gearify-umbrella/localstack/scripts/seed-featured-products.sh

#!/bin/bash

# Feature top 3 cricket bats for homepage
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test aws dynamodb update-item \
  --table-name gearify-products \
  --endpoint-url http://localhost:4566 \
  --region us-east-1 \
  --key '{"PK": {"S": "TENANT#default"}, "SK": {"S": "PRODUCT#prod-bat-ss-001"}}' \
  --update-expression "SET IsFeatured = :true, FeaturedPriority = :p1, FeaturedSections = :sections" \
  --expression-attribute-values '{
    ":true": {"BOOL": true},
    ":p1": {"N": "1"},
    ":sections": {"SS": ["homepage", "cricket", "best-sellers"]}
  }'

AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test aws dynamodb update-item \
  --table-name gearify-products \
  --endpoint-url http://localhost:4566 \
  --region us-east-1 \
  --key '{"PK": {"S": "TENANT#default"}, "SK": {"S": "PRODUCT#prod-bat-mrf-001"}}' \
  --update-expression "SET IsFeatured = :true, FeaturedPriority = :p2, FeaturedSections = :sections" \
  --expression-attribute-values '{
    ":true": {"BOOL": true},
    ":p2": {"N": "2"},
    ":sections": {"SS": ["homepage", "cricket"]}
  }'

# Feature deals section
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test aws dynamodb update-item \
  --table-name gearify-products \
  --endpoint-url http://localhost:4566 \
  --region us-east-1 \
  --key '{"PK": {"S": "TENANT#default"}, "SK": {"S": "PRODUCT#prod-deal-clearance-001"}}' \
  --update-expression "SET IsFeatured = :true, FeaturedPriority = :p1, FeaturedSections = :sections, FeaturedUntil = :until" \
  --expression-attribute-values '{
    ":true": {"BOOL": true},
    ":p1": {"N": "1"},
    ":sections": {"SS": ["deals", "clearance"]},
    ":until": {"S": "2026-01-31T23:59:59.000Z"}
  }'

echo "Featured products seeded successfully!"
```

---

## Deployment Checklist

- [ ] Update `Product` domain entity with featured fields
- [ ] Run migration script to add featured fields to existing products
- [ ] Implement `GetFeaturedProductsQuery` and handler
- [ ] Implement `UpdateProductFeaturedStatusCommand` and handler
- [ ] Add API endpoints to `ProductsController`
- [ ] Create `FeaturedProductsService` in Angular
- [ ] Update homepage component to load featured products
- [ ] Create admin interface for managing featured products
- [ ] Add unit tests for query/command handlers
- [ ] Add API integration tests
- [ ] Seed sample featured products
- [ ] Test on LocalStack
- [ ] Deploy to staging
- [ ] Load test featured products endpoint
- [ ] Deploy to production
- [ ] Monitor CloudWatch metrics

---

## Future Enhancements

1. **Auto-rotation**: Automatically rotate featured products weekly
2. **A/B Testing**: Test different featured product sets
3. **Analytics**: Track featured product performance (CTR, conversion)
4. **Smart Featuring**: AI-based auto-selection of featured products
5. **Personalized Featured**: Show different featured products per user segment
6. **Seasonal**: Auto-feature products for upcoming seasons/events

---

**Created**: January 2026
**Last Updated**: January 2026
**Status**: Ready for Implementation
