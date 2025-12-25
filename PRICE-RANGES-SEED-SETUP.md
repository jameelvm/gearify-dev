# Price Ranges - Automatic Seeding Setup

## ✅ What Was Configured

Your LocalStack environment will now **automatically** create and seed the price ranges table when it starts!

### Files Created/Modified:

1. **Table Definition**
   - `gearify-umbrella/localstack/dynamodb/tables/price-ranges.json`
   - Defines the table schema (PK/SK structure)

2. **Seed Data**
   - `gearify-umbrella/localstack/dynamodb/data/price-ranges-default-tenant.json`
   - Contains 5 default price ranges with GUIDs for 'default' tenant

3. **Initialization Script** (Updated)
   - `gearify-umbrella/localstack/init-aws.sh`
   - Line 177-189: Creates gearify-price-ranges table
   - Line 241-246: Seeds default tenant price ranges

4. **Manual Setup Script** (Enhanced with logging)
   - `setup-price-ranges-table.sh` (in root)
   - Now includes detailed progress logging
   - Shows success/failure count
   - Can be run manually if needed

---

## 🚀 How to Use

### Option 1: Automatic (Recommended)

**Just restart LocalStack and it will auto-seed:**

```bash
cd gearify-umbrella
docker-compose restart localstack
```

Watch the logs to see the seeding:
```bash
docker logs -f gearify-localstack
```

You should see:
```
Initializing LocalStack AWS Resources
...
  - Creating table: gearify-price-ranges
  - Seeding price ranges for default tenant
...
LocalStack initialization completed!
```

---

### Option 2: Manual Setup

If you need to run it manually:

```bash
cd C:\Gearify
bash setup-price-ranges-table.sh
```

**Output Example:**
```
=========================================
  GEARIFY PRICE RANGES TABLE SETUP
=========================================

Configuration:
  • Endpoint: http://localhost:4566
  • Region: us-east-1
  • Table: gearify-price-ranges

Step 1: Creating DynamoDB table...
----------------------------------------
✓ Table 'gearify-price-ranges' created successfully

Step 2: Seeding default tenant price ranges...
----------------------------------------
✓ [1/5] Added: Under $50
✓ [2/5] Added: $50-$100
✓ [3/5] Added: $100-$250
✓ [4/5] Added: $250-$500
✓ [5/5] Added: $500 & Above

=========================================
  INSERTION SUMMARY
=========================================
  Successfully inserted: 5/5 records
  Status: ✓ ALL RECORDS INSERTED

Step 3: Verifying data...
----------------------------------------
[Shows table with all 5 price ranges]

=========================================
  SETUP COMPLETE!
=========================================
  Table: gearify-price-ranges
  Tenant: default
  Records inserted: 5/5

✓ All price ranges seeded successfully!
=========================================
```

---

## 📊 Seeded Price Ranges

| ID | Label | Min Price | Max Price | Display Order |
|----|-------|-----------|-----------|---------------|
| a1b2c3d4-e5f6-7890-abcd-ef1234567890 | Under $50 | $0 | $50 | 1 |
| b2c3d4e5-f6a7-8901-bcde-f12345678901 | $50-$100 | $50 | $100 | 2 |
| c3d4e5f6-a7b8-9012-cdef-123456789012 | $100-$250 | $100 | $250 | 3 |
| d4e5f6a7-b8c9-0123-def1-234567890123 | $250-$500 | $250 | $500 | 4 |
| e5f6a7b8-c9d0-1234-ef12-345678901234 | $500 & Above | $500 | (none) | 5 |

All ranges are configured for:
- **Tenant**: default
- **Currency**: USD
- **Status**: Active

---

## 🔍 Verification

### 1. Check if table exists:
```bash
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test \
aws dynamodb describe-table \
    --table-name gearify-price-ranges \
    --endpoint-url http://localhost:4566 \
    --region us-east-1
```

### 2. Count items in table:
```bash
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test \
aws dynamodb scan \
    --table-name gearify-price-ranges \
    --select COUNT \
    --endpoint-url http://localhost:4566 \
    --region us-east-1
```

Expected output: `"Count": 5`

### 3. Query price ranges for default tenant:
```bash
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test \
aws dynamodb query \
    --table-name gearify-price-ranges \
    --key-condition-expression "PK = :pk AND begins_with(SK, :sk)" \
    --expression-attribute-values '{
        ":pk": {"S": "TENANT#default"},
        ":sk": {"S": "PRICERANGE#"}
    }' \
    --endpoint-url http://localhost:4566 \
    --region us-east-1
```

### 4. Test API endpoint:
```bash
curl -H "X-Tenant-Id: default" \
     http://localhost:8080/api/catalog/price-ranges
```

Expected response:
```json
[
  {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "label": "Under $50",
    "minPrice": 0,
    "maxPrice": 50,
    "currency": "USD",
    "displayOrder": 1,
    "productCount": 42,
    "value": "0-50"
  },
  ...
]
```

---

## 🔧 Troubleshooting

### Price ranges not showing in frontend?

1. **Check LocalStack logs:**
   ```bash
   docker logs gearify-localstack | grep -i "price"
   ```

2. **Verify table exists:**
   ```bash
   AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test \
   awslocal dynamodb list-tables
   ```
   Should include: `gearify-price-ranges`

3. **Check if data was seeded:**
   ```bash
   docker logs gearify-localstack | grep "Seeding price ranges"
   ```
   Should show: `- Seeding price ranges for default tenant`

4. **Restart catalog service:**
   ```bash
   docker-compose restart catalog-svc
   ```

5. **Check browser console:**
   - Open Developer Tools (F12)
   - Look for API calls to `/api/catalog/price-ranges`
   - Check for errors

### Table exists but no data?

Run the manual seed script:
```bash
bash setup-price-ranges-table.sh
```

### Seeding fails with error?

Check LocalStack health:
```bash
curl http://localhost:4566/_localstack/health
```

---

## 📝 Adding Price Ranges for Other Tenants

To add price ranges for a different tenant (e.g., "acme"):

1. **Create new seed file:**
   ```bash
   cp gearify-umbrella/localstack/dynamodb/data/price-ranges-default-tenant.json \
      gearify-umbrella/localstack/dynamodb/data/price-ranges-acme-tenant.json
   ```

2. **Edit the file and replace:**
   - `"TENANT#default"` → `"TENANT#acme"`
   - `"TenantId": {"S": "default"}` → `"TenantId": {"S": "acme"}`
   - Generate new GUIDs for IDs

3. **Add to init-aws.sh:**
   ```bash
   # Seed price ranges for acme tenant
   echo "  - Seeding price ranges for acme tenant"
   awslocal dynamodb batch-write-item \
     --request-items file://${CONFIG_DIR}/dynamodb/data/price-ranges-acme-tenant.json \
     --region us-east-1 \
     2>/dev/null || echo "    Failed to seed price ranges for acme"
   ```

4. **Restart LocalStack:**
   ```bash
   docker-compose restart localstack
   ```

---

## ✨ Summary

✅ **Auto-seeding configured** - Price ranges load automatically on LocalStack startup
✅ **5 default ranges** - Seeded for 'default' tenant with GUIDs
✅ **Detailed logging** - Both automatic and manual scripts show progress
✅ **Easy verification** - Multiple commands to verify setup
✅ **Multi-tenant ready** - Easy to add ranges for other tenants

**Next time you restart LocalStack, price ranges will be there automatically!** 🎉
