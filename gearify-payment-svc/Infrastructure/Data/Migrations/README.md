# EF Core Migrations - Payment Service

This folder contains Entity Framework Core migrations for the Payment Service database schema.

## Prerequisites

Install EF Core tools globally (one-time setup):
```bash
dotnet tool install --global dotnet-ef
```

## Common Commands

### Add a New Migration

After making changes to entities or DbContext configuration:

```bash
cd gearify-payment-svc
dotnet ef migrations add <MigrationName> --output-dir Infrastructure/Data/Migrations
```

**Example:**
```bash
dotnet ef migrations add AddPaymentRetryCount --output-dir Infrastructure/Data/Migrations
```

**Naming conventions:**
- Use PascalCase
- Be descriptive: `AddPaymentRetryCount`, `UpdateRefundStatus`, `AddWebhookEvents`
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
| `payment_transactions` | Payment records with provider details |
| `payment_ledger` | Double-entry ledger for financial tracking |
| `refunds` | Refund records linked to transactions |

### Key Indexes

- `ix_payment_transactions_tenant_id` - Multi-tenant queries
- `ix_payment_transactions_order_id` - Order payment lookup
- `ix_payment_transactions_idempotency_key` - Idempotent request handling
- `ix_payment_transactions_tenant_created` - Tenant + date queries
- `ix_refunds_transaction_id` - Refund lookups by transaction

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
  "ConnectionStrings": {
    "PaymentDb": "Host=localhost;Port=5432;Database=gearify_payments;Username=postgres;Password=postgres"
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

## Payment-Specific Considerations

### Idempotency

The `idempotency_key` column ensures duplicate payment requests are handled safely. Always include this in payment processing.

### Ledger Entries

The `payment_ledger` table uses double-entry bookkeeping:
- `credit` - Money received (payment success)
- `debit` - Money returned (refunds)

### Refund Tracking

Partial refunds are supported. The `refunds` table tracks:
- Individual refund amounts
- Provider refund IDs for reconciliation
- Refund status lifecycle
