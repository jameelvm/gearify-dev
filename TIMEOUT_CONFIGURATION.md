# API Gateway Timeout Configuration

## Problem

When debugging with breakpoints, requests can take longer than the default timeout (usually 100 seconds), causing:
- Client shows timeout error
- Request fails even though your code is still running
- Frustrating debugging experience

## Solution

I've increased the timeouts to **10 minutes** in Development mode.

---

## What Was Changed

**File:** `C:\Gearify\gearify-api-gateway\appsettings.Development.json`

### 1. YARP Request Timeout

```json
"HttpRequest": {
  "Timeout": "00:10:00"  // 10 minutes
}
```

This controls how long YARP waits for the backend service to respond.

### 2. Kestrel Timeouts

```json
"Kestrel": {
  "Limits": {
    "KeepAliveTimeout": "00:10:00",      // Keep connection alive for 10 min
    "RequestHeadersTimeout": "00:10:00"  // Wait 10 min for headers
  }
}
```

This controls how long the API Gateway itself waits for requests.

---

## Timeout Breakdown

### Default Timeouts (Too Short for Debugging)
- YARP proxy timeout: **100 seconds**
- Kestrel keep-alive: **130 seconds**
- Request headers: **30 seconds**

### New Timeouts (Good for Debugging)
- YARP proxy timeout: **600 seconds (10 minutes)**
- Kestrel keep-alive: **600 seconds (10 minutes)**
- Request headers: **600 seconds (10 minutes)**

---

## When These Timeouts Apply

**Applies only in Development environment** (appsettings.Development.json)

This means:
- ✅ When debugging locally - you get 10 minutes
- ❌ In production - default timeouts apply (safer)

---

## How to Change Timeout

If 10 minutes is too short/long, edit the timeout value:

```json
"Timeout": "00:10:00"  // Format: HH:MM:SS
```

**Examples:**
- `"00:05:00"` = 5 minutes
- `"00:15:00"` = 15 minutes
- `"01:00:00"` = 1 hour

---

## Other Timeout Locations

### 1. Angular (Client-Side)

**File:** `C:\Gearify\gearify-web\src\app\core\interceptors\http-error.interceptor.ts`

Angular's HttpClient default timeout is usually **0** (no timeout), but if you set one:

```typescript
this.http.get(url, {
  timeout: 600000  // 10 minutes in milliseconds
})
```

### 2. Nginx (Web Server)

**File:** In the nginx config (if you have custom timeout settings)

```nginx
proxy_read_timeout 600s;  # 10 minutes
proxy_connect_timeout 600s;
proxy_send_timeout 600s;
```

But typically Nginx uses defaults which are long enough.

### 3. Individual Service Timeouts

Each backend service also has its own timeout for database/external calls.

**Example - Auth Service:**
```csharp
// DynamoDB client timeout
var config = new AmazonDynamoDBConfig
{
    Timeout = TimeSpan.FromMinutes(10)
};
```

---

## What Happens Now

### Before (Default):
```
You set breakpoint → pause for 2 minutes → Client timeout after 100 sec ❌
```

### After (10 minute timeout):
```
You set breakpoint → pause for 2 minutes → Still works, 8 min remaining ✅
```

---

## Production Consideration

**Important:** These long timeouts are ONLY for development!

In production, you want **short timeouts** to:
- Fail fast if service is down
- Avoid tying up resources
- Better user experience (don't wait 10 minutes for error)

The production config (appsettings.json) still uses default shorter timeouts.

---

## Testing the Timeout

1. Set a breakpoint in your auth service
2. Trigger a login from the web UI
3. Pause at the breakpoint for 2-3 minutes
4. Continue execution
5. Request should complete successfully (no timeout)

---

## Quick Reference

**Timeout Format:** `"HH:MM:SS"`

| Value | Duration |
|-------|----------|
| `"00:01:00"` | 1 minute |
| `"00:05:00"` | 5 minutes |
| `"00:10:00"` | 10 minutes |
| `"00:30:00"` | 30 minutes |
| `"01:00:00"` | 1 hour |

**Where to Change:**
- `C:\Gearify\gearify-api-gateway\appsettings.Development.json`
- Restart API Gateway: `docker compose restart api-gateway`

---

## Summary

✅ **Timeout increased to 10 minutes** in Development mode
✅ **Only affects debugging** - production uses default
✅ **Applies to both YARP and Kestrel** - covers all timeout scenarios
✅ **Already configured** - just restart API Gateway to apply

Happy debugging without timeout stress! 🎯
