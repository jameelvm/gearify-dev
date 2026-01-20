# EF Core Migrations - Order Service

This folder contains Entity Framework Core migrations for the Order Service database schema.

## Prerequisites

Install EF Core tools globally (one-time setup):
```bash
dotnet tool install --global dotnet-ef
```

## Common Commands

### Add a New Migration

After making changes to entities or DbContext configuration:

```bash
cd gearify-order-svc
dotnet ef migrations add <MigrationName> --output-dir Infrastructure/Data/Migrations
```

**Example:**
```bash
dotnet ef migrations add AddShippingTrackingFields --output-dir Infrastructure/Data/Migrations
```

**Naming conventions:**
- Use PascalCase
- Be descriptive: `AddShippingTrackingFields`, `UpdateOrderStatusEnum`, `AddPaymentRetryCount`
- Prefix with action: `Add`, `Update`, `Remove`, `Rename`

### Apply Migrations to Database

```bash
# Apply all pending migrations
dotnet ef database update

# Apply up to a specific migration
dotnet ef database update <MigrationName>
```

### Remove Last Migration

If the migration hasn't been applied to the database yet:

```bash
dotnet ef migrations remove
```

### Revert Database to Previous Migration

```bash
dotnet ef database update <PreviousMigrationName>
```

To revert all migrations:
```bash
dotnet ef database update 0
```

### Generate SQL Script

For production deployments or review:

```bash
# Generate script for all migrations
dotnet ef migrations script --output migrations.sql

# Generate script from specific migration
dotnet ef migrations script <FromMigration> <ToMigration> --output migrations.sql

# Generate idempotent script (safe to run multiple times)
dotnet ef migrations script --idempotent --output migrations.sql
```

### List All Migrations

```bash
dotnet ef migrations list
```

## Database Schema

### Tables

| Table | Description |
|-------|-------------|
| `orders` | Main order records with status, amounts, addresses |
| `order_items` | Line items for each order |
| `order_status_history` | Audit trail of status changes |

### Key Indexes

- `idx_orders_tenant_id` - Multi-tenant queries
- `idx_orders_user_id` - User order lookups
- `idx_orders_status` - Status filtering
- `idx_orders_created_at` - Date range queries
- `IX_orders_order_number` - Unique order number lookup

## Best Practices

1. **Always review generated migrations** before applying
2. **Test migrations** on a development database first
3. **Never modify** already-applied migrations
4. **Create focused migrations** - one logical change per migration
5. **Use meaningful names** that describe the change
6. **Backup database** before applying migrations in production

## Troubleshooting

### Migration not detecting changes

```bash
# Rebuild the project first
dotnet build
dotnet ef migrations add <MigrationName> --output-dir Infrastructure/Data/Migrations
```

### Connection string issues

Ensure `appsettings.json` or environment variable `POSTGRES_CONNECTION_STRING` is set:

```json
{
  "DatabaseConfiguration": {
    "ConnectionString": "Host=localhost;Port=5432;Database=gearify_orders;Username=postgres;Password=postgres"
  }
}
```

### Pending model changes warning

If you see "The model has changed since the last migration", create a new migration to capture the changes.

## Startup Configuration

Migrations are automatically applied on startup in development mode via:

```csharp
dbContext.Database.EnsureCreated();
```

For production, use explicit migration:

```csharp
dbContext.Database.Migrate();
```
