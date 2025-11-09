# Subdomain-Based Multi-Tenancy Setup Guide

## Overview

Gearify now supports **hybrid multi-tenancy resolution**:
- **Web Apps**: Subdomain-based (e.g., `acme.localhost.direct:4200`)
- **API Calls**: Header-based fallback (e.g., `X-Tenant-Id: acme`)
- **Mobile/Services**: Header-based (continues to work as before)

## How It Works

### Architecture Flow
```
User accesses: acme.localhost.direct:4200
    ↓
Angular App detects subdomain "acme"
    ↓
Stores tenant in localStorage
    ↓
Sends API request with X-Tenant-Id: acme header
    ↓
API Gateway receives request
    ↓
Gateway extracts tenant from subdomain OR header
    ↓
Forwards to microservices with X-Tenant-Id header
    ↓
Services use tenant for data isolation
```

### Resolution Priority

**Angular Frontend:**
1. Check localStorage for `tenant_id` (manual override)
2. Extract from subdomain (e.g., `acme.localhost.direct`)
3. Default to `"default"`

**API Gateway:**
1. Check existing `X-Tenant-Id` header (for API/mobile clients)
2. Extract from subdomain
3. Default to `"default"`

## Local Development Setup

### Option 1: Using localhost.direct (Recommended - No Setup Required)

**Default Tenant:**
```
http://default.localhost.direct:4200
```

**Custom Tenants:**
```
http://acme.localhost.direct:4200
http://contoso.localhost.direct:4200
http://fabrikam.localhost.direct:4200
```

**How it works:**
- `localhost.direct` is a free service that resolves `*.localhost.direct` to `127.0.0.1`
- No DNS configuration needed
- Works immediately

### Option 2: Using localtest.me

**Default Tenant:**
```
http://default.localtest.me:4200
```

**Custom Tenants:**
```
http://acme.localtest.me:4200
http://contoso.localtest.me:4200
```

**How it works:**
- `localtest.me` resolves `*.localtest.me` to `127.0.0.1`
- No setup required

### Option 3: Custom Hosts File (Advanced)

**1. Edit your hosts file:**

**Windows:** `C:\Windows\System32\drivers\etc\hosts`
**Mac/Linux:** `/etc/hosts`

**2. Add entries:**
```
127.0.0.1 default.localdev.gearify.com
127.0.0.1 acme.localdev.gearify.com
127.0.0.1 contoso.localdev.gearify.com
```

**3. Access via:**
```
http://default.localdev.gearify.com:4200
http://acme.localdev.gearify.com:4200
```

## Starting Your Development Environment

### 1. Start Backend Services (Docker)

```bash
cd gearify-umbrella
docker-compose up -d
```

Services will be available at:
- API Gateway: `http://localhost:8080`
- Individual microservices: See docker-compose.yml for ports

### 2. Start Auth Service in Visual Studio (for debugging)

- Open `gearify-auth-svc` in Visual Studio
- Press F5 or Start Debugging
- Service runs on `http://0.0.0.0:5011`

### 3. Start Angular App

```bash
cd gearify-web
npm start
```

**Important:** Configure Angular dev server to listen on all interfaces by editing `package.json`:

```json
{
  "scripts": {
    "start": "ng serve --host 0.0.0.0 --port 4200"
  }
}
```

### 4. Access Your App

**Default Tenant:**
```
http://default.localhost.direct:4200
```

**Acme Tenant:**
```
http://acme.localhost.direct:4200
```

**Contoso Tenant:**
```
http://contoso.localhost.direct:4200
```

## Testing Multi-Tenancy

### Test 1: Register Users in Different Tenants

**Tenant: Default**
1. Go to `http://default.localhost.direct:4200`
2. Register user: `admin@default.com`
3. User stored with `TenantId: "default"`

**Tenant: Acme**
1. Go to `http://acme.localhost.direct:4200`
2. Register user: `admin@acme.com`
3. User stored with `TenantId: "acme"`

**Tenant: Contoso**
1. Go to `http://contoso.localhost.direct:4200`
2. Register user: `admin@contoso.com`
3. User stored with `TenantId: "contoso"`

### Test 2: Verify Data Isolation

**Check DynamoDB:**
```
Users table structure:
- PK: TENANT#default, SK: USER#user-id-1 -> admin@default.com
- PK: TENANT#acme, SK: USER#user-id-2 -> admin@acme.com
- PK: TENANT#contoso, SK: USER#user-id-3 -> admin@contoso.com
```

Each tenant's data is completely isolated!

### Test 3: Manual Tenant Override (Testing)

Open browser console and run:
```javascript
// Switch to different tenant
localStorage.setItem('tenant_id', 'test-tenant');
location.reload();

// Check current tenant
console.log(localStorage.getItem('tenant_id'));

// Clear to revert to subdomain detection
localStorage.removeItem('tenant_id');
location.reload();
```

## API Testing with Direct HTTP Calls

### Using cURL

```bash
# Register user in default tenant
curl -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -d '{
    "email": "test@default.com",
    "password": "Test@1234",
    "firstName": "Test",
    "lastName": "User"
  }'

# Register user in acme tenant
curl -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: acme" \
  -d '{
    "email": "test@acme.com",
    "password": "Test@1234",
    "firstName": "Test",
    "lastName": "User"
  }'
```

### Using Postman

**Headers:**
```
Content-Type: application/json
X-Tenant-Id: acme
```

**Body:**
```json
{
  "email": "test@acme.com",
  "password": "Test@1234",
  "firstName": "Test",
  "lastName": "User"
}
```

## Mobile App / External API Integration

Mobile apps and external services can continue using header-based tenancy:

```javascript
// Mobile app example
fetch('http://api.gearify.com/api/auth/login', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'X-Tenant-Id': 'acme'  // Specify tenant in header
  },
  body: JSON.stringify({
    email: 'user@acme.com',
    password: 'password123'
  })
});
```

## Production Deployment

### DNS Setup

**1. Configure wildcard DNS:**
```
*.gearify.com -> Your Load Balancer IP
```

**2. SSL Certificate:**
- Use wildcard certificate: `*.gearify.com`
- Or use Let's Encrypt with DNS-01 challenge for wildcard

**3. Access patterns:**
```
https://acme.gearify.com -> Tenant: acme
https://contoso.gearify.com -> Tenant: contoso
https://www.gearify.com -> Tenant: default (marketing site)
```

### Environment Variables

**API Gateway:**
```
ASPNETCORE_URLS=http://0.0.0.0:80
ASPNETCORE_ENVIRONMENT=Production
```

**Angular App:**
```typescript
// environment.prod.ts
export const environment = {
  production: true,
  apiUrl: 'https://api.gearify.com'  // API subdomain
};
```

## Troubleshooting

### Issue: "Missing X-Tenant-Id header" error

**Solution:**
- Check if you're accessing via subdomain (e.g., `acme.localhost.direct`)
- Clear localStorage: `localStorage.removeItem('tenant_id')`
- Check API Gateway logs for tenant resolution

### Issue: Subdomain not detected in Angular

**Solution:**
- Verify you're NOT using plain `localhost:4200`
- Use `default.localhost.direct:4200` instead
- Clear browser cache and localStorage

### Issue: CORS errors with subdomain

**Solution:**
- API Gateway already configured to accept subdomain origins
- Restart API Gateway container if you changed configuration

### Issue: Can't access subdomain URLs

**Solution:**
- Ping the subdomain: `ping acme.localhost.direct`
- Should resolve to `127.0.0.1`
- If not, try `localtest.me` or configure hosts file

## Reserved Subdomains

These subdomains will NOT be treated as tenants:
- `www` - Marketing/public site
- `api` - API Gateway
- `admin` - Admin portal
- `app` - Generic app subdomain
- `localhost` - Development

## Summary

✅ **Web Users**: Professional subdomain experience
✅ **API Clients**: Simple header-based tenancy
✅ **Developers**: Easy local setup with `localhost.direct`
✅ **Services**: Unchanged - use `X-Tenant-Id` header
✅ **Data Isolation**: Complete separation per tenant in DynamoDB

Your multi-tenant architecture is now production-ready!
