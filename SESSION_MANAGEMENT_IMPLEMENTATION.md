# Session Management Implementation Guide

**Date**: November 27, 2025
**Version**: 1.0
**Status**: ✅ Backend Complete | ⏳ Frontend Pending

---

## Overview

Multi-device session management has been successfully integrated into the Gearify Auth Service. Users can now:
- Login from multiple devices simultaneously
- View all active sessions
- Logout from specific devices
- Logout from all devices at once

---

## Backend Changes (✅ Complete)

### 1. Login Flow (`gearify-auth-svc/Application/Commands/LoginCommandHandler.cs:135-145`)

Sessions are now created on every login:

```csharp
// Create session for multi-device tracking
var deviceInfo = request.DeviceInfo ?? "Unknown Device";
var ipAddress = request.IpAddress ?? "Unknown IP";

await _sessionService.CreateSessionAsync(
    user.Id,
    user.TenantId,
    refreshToken,
    deviceInfo,
    ipAddress
);
```

**Device Info Captured**: User-Agent header from browser/app
**IP Address Captured**: Client's IP from HTTP context

### 2. Token Refresh Flow (`gearify-auth-svc/Application/Commands/RefreshTokenCommandHandler.cs:49-93`)

Now validates sessions and implements token rotation:

```csharp
// Validate session using refresh token
var session = await _sessionService.ValidateSessionAsync(user.Id, request.RefreshToken);
if (session == null)
{
    return new RefreshTokenResult(string.Empty, string.Empty, false, "Invalid or expired session");
}

// Clean up expired sessions
await _sessionService.CleanupExpiredSessionsAsync(user.Id);

// Generate new tokens and rotate session
var accessToken = _jwtService.GenerateAccessToken(user);
var newRefreshToken = _jwtService.GenerateRefreshToken();

// Revoke old session, create new one with rotated token
await _sessionService.RevokeSessionAsync(user.Id, session.Id);
await _sessionService.CreateSessionAsync(user.Id, user.TenantId, newRefreshToken, session.DeviceInfo, session.IpAddress, session.Location);
```

**Security**: Old refresh token is invalidated, new session created

### 3. Logout & Session Endpoints

**Auth Controller** (`gearify-auth-svc/API/Controllers/AuthController.cs:257-287`):

| Endpoint | Method | Description | Auth Required |
|----------|--------|-------------|---------------|
| `/api/auth/logout` | POST | Logout from current session | ✅ Bearer Token |

**Request Body**:
```json
{
  "refreshToken": "base64-encoded-refresh-token"
}
```

**Session Controller** (`gearify-auth-svc/API/Controllers/SessionController.cs`):

| Endpoint | Method | Description | Auth Required |
|----------|--------|-------------|---------------|
| `/api/session/active` | GET | Get all active sessions for current user | ✅ Bearer Token |
| `/api/session/revoke/{sessionId}` | POST | Logout from specific device | ✅ Bearer Token |
| `/api/session/revoke-all` | POST | Logout from all devices | ✅ Bearer Token |

### 4. AWS LocalStack Configuration Fix

**Problem**: Auth service couldn't connect to LocalStack from Docker container
**Solution**: Added environment variable override in `Startup.cs:150-156`

```csharp
var dynamoDbEndpoint = Environment.GetEnvironmentVariable("DYNAMODB_ENDPOINT");
if (!string.IsNullOrEmpty(dynamoDbEndpoint))
{
    awsOptions.DefaultClientConfig.ServiceURL = dynamoDbEndpoint;
}
```

---

## Database Schema

### UserSessions Table

| Field | Type | Description |
|-------|------|-------------|
| PK | String | `TENANT#{tenantId}` |
| SK | String | `SESSION#{sessionId}` |
| Id | String | Session GUID |
| UserId | String | User GUID |
| TenantId | String | Tenant identifier |
| RefreshToken | String | BCrypt hashed refresh token |
| DeviceInfo | String | User-Agent string |
| IpAddress | String | Client IP address |
| Location | String | (Optional) Geo-location |
| CreatedAt | DateTime | Session creation time |
| LastAccessedAt | DateTime | Last token refresh time |
| ExpiresAt | DateTime | Session expiry (7 days default) |
| IsActive | Boolean | Session status |

**GSI1**: `UserId` (PK) + `CreatedAt` (SK) → Get all sessions for user
**GSI2**: `RefreshToken` (PK) → Validate refresh token

---

## Frontend Integration (⏳ Pending)

### Required Changes

#### 1. Auth Service/Store

Update your authentication service to handle sessions:

```typescript
// src/app/services/auth.service.ts

interface LoginResponse {
  token: string;            // Access token (15 min expiry)
  refreshToken: string;     // Refresh token (7 day expiry)
  user: User;
}

interface Session {
  sessionId: string;
  deviceInfo: string;
  ipAddress: string;
  location?: string;
  createdAt: Date;
  lastAccessedAt: Date;
  expiresAt: Date;
  isCurrent: boolean;       // True for current session
}

@Injectable({ providedIn: 'root' })
export class AuthService {

  // Existing login - no changes needed
  async login(email: string, password: string): Promise<LoginResponse> {
    const response = await this.http.post<LoginResponse>('/api/auth/login', {
      email,
      password
    }).toPromise();

    // Store tokens (no changes)
    localStorage.setItem('access_token', response.token);
    localStorage.setItem('refresh_token', response.refreshToken);

    return response;
  }

  // NEW: Logout from current session
  async logout(): Promise<void> {
    const refreshToken = localStorage.getItem('refresh_token');
    if (refreshToken) {
      try {
        await this.http.post('/api/auth/logout', {
          refreshToken
        }).toPromise();
      } catch (error) {
        console.error('Logout failed', error);
      }
    }

    // Clear tokens from storage
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');
  }

  // NEW: Get active sessions
  async getActiveSessions(): Promise<Session[]> {
    return this.http.get<Session[]>('/api/session/active').toPromise();
  }

  // NEW: Logout from specific session (other device)
  async logoutSession(sessionId: string): Promise<void> {
    await this.http.post(`/api/session/revoke/${sessionId}`, {}).toPromise();
  }

  // NEW: Logout from all devices
  async logoutAllSessions(): Promise<void> {
    await this.http.post('/api/session/revoke-all', {}).toPromise();
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');
  }
}
```

#### 2. Sessions Management Component

Create a new component to display active sessions:

```typescript
// src/app/components/sessions/sessions.component.ts

@Component({
  selector: 'app-sessions',
  templateUrl: './sessions.component.html'
})
export class SessionsComponent implements OnInit {
  sessions: Session[] = [];
  loading = false;

  constructor(private authService: AuthService) {}

  async ngOnInit() {
    await this.loadSessions();
  }

  async loadSessions() {
    this.loading = true;
    try {
      this.sessions = await this.authService.getActiveSessions();
    } finally {
      this.loading = false;
    }
  }

  async revokeSession(sessionId: string) {
    if (confirm('Are you sure you want to logout from this device?')) {
      await this.authService.logoutSession(sessionId);
      await this.loadSessions();
    }
  }

  async revokeAllSessions() {
    if (confirm('This will logout from ALL devices. Continue?')) {
      await this.authService.logoutAllSessions();
      // Redirect to login
      this.router.navigate(['/login']);
    }
  }

  getDeviceName(deviceInfo: string): string {
    // Parse User-Agent to friendly name
    if (deviceInfo.includes('iPhone')) return 'iPhone';
    if (deviceInfo.includes('iPad')) return 'iPad';
    if (deviceInfo.includes('Android')) return 'Android Device';
    if (deviceInfo.includes('Chrome')) return 'Chrome Browser';
    if (deviceInfo.includes('Firefox')) return 'Firefox Browser';
    if (deviceInfo.includes('Safari')) return 'Safari Browser';
    return 'Unknown Device';
  }

  getDeviceIcon(deviceInfo: string): string {
    if (deviceInfo.includes('Mobile') || deviceInfo.includes('iPhone')) return 'smartphone';
    if (deviceInfo.includes('iPad') || deviceInfo.includes('Tablet')) return 'tablet';
    return 'computer';
  }
}
```

#### 3. Sessions Template

```html
<!-- src/app/components/sessions/sessions.component.html -->

<div class="sessions-container">
  <h2>Active Sessions</h2>
  <p class="subtitle">Manage devices where you're currently logged in</p>

  <div *ngIf="loading" class="loading">Loading sessions...</div>

  <div *ngIf="!loading && sessions.length === 0" class="no-sessions">
    <p>No active sessions found</p>
  </div>

  <div class="sessions-list">
    <div *ngFor="let session of sessions"
         class="session-card"
         [class.current]="session.isCurrent">

      <div class="session-icon">
        <mat-icon>{{ getDeviceIcon(session.deviceInfo) }}</mat-icon>
      </div>

      <div class="session-details">
        <div class="device-name">
          {{ getDeviceName(session.deviceInfo) }}
          <span *ngIf="session.isCurrent" class="current-badge">Current</span>
        </div>
        <div class="session-info">
          <span class="ip-address">{{ session.ipAddress }}</span>
          <span class="separator">•</span>
          <span class="location" *ngIf="session.location">{{ session.location }}</span>
        </div>
        <div class="session-time">
          Last active: {{ session.lastAccessedAt | date:'short' }}
        </div>
      </div>

      <button
        *ngIf="!session.isCurrent"
        mat-button
        color="warn"
        (click)="revokeSession(session.sessionId)">
        Logout
      </button>
    </div>
  </div>

  <div class="actions">
    <button
      mat-raised-button
      color="warn"
      (click)="revokeAllSessions()">
      Logout From All Devices
    </button>
  </div>
</div>
```

#### 4. Token Refresh Interceptor (Update)

Your existing token refresh logic should work, but ensure it handles session rotation:

```typescript
// src/app/interceptors/auth.interceptor.ts

@Injectable()
export class AuthInterceptor implements HttpInterceptor {

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    return next.handle(req).pipe(
      catchError((error: HttpErrorResponse) => {
        if (error.status === 401 && error.headers.get('Token-Expired')) {
          // Token expired, refresh it
          return this.authService.refreshToken().pipe(
            switchMap(() => {
              // Retry original request with new token
              const cloned = req.clone({
                setHeaders: {
                  Authorization: `Bearer ${this.authService.getAccessToken()}`
                }
              });
              return next.handle(cloned);
            }),
            catchError(refreshError => {
              // Refresh failed, logout
              this.authService.logout();
              return throwError(refreshError);
            })
          );
        }
        return throwError(error);
      })
    );
  }
}
```

#### 5. User Settings Menu

Add "Active Sessions" link to user settings/profile menu:

```html
<!-- src/app/components/user-menu/user-menu.component.html -->

<mat-menu #userMenu="matMenu">
  <button mat-menu-item routerLink="/profile">
    <mat-icon>person</mat-icon>
    Profile
  </button>

  <!-- NEW -->
  <button mat-menu-item routerLink="/sessions">
    <mat-icon>devices</mat-icon>
    Active Sessions
  </button>

  <button mat-menu-item (click)="logout()">
    <mat-icon>logout</mat-icon>
    Logout
  </button>
</mat-menu>
```

#### 6. Routing

Add route for sessions page:

```typescript
// src/app/app-routing.module.ts

const routes: Routes = [
  // ... existing routes
  {
    path: 'sessions',
    component: SessionsComponent,
    canActivate: [AuthGuard]
  }
];
```

---

## Testing the Integration

### Backend Testing (✅ Verified)

```bash
# 1. Register a user
curl -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "User-Agent: Mozilla/5.0 (iPhone)" \
  --data @test-register.json

# 2. Login (creates session)
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "User-Agent: Mozilla/5.0 (iPad)" \
  --data @test-login.json

# 3. Get active sessions
curl -X GET http://localhost:8080/api/session/active \
  -H "Authorization: Bearer {ACCESS_TOKEN}" \
  -H "X-Tenant-Id: default"

# 4. Logout from specific session
curl -X POST http://localhost:8080/api/session/revoke/{SESSION_ID} \
  -H "Authorization: Bearer {ACCESS_TOKEN}" \
  -H "X-Tenant-Id: default"

# 5. Logout from all devices
curl -X POST http://localhost:8080/api/session/revoke-all \
  -H "Authorization: Bearer {ACCESS_TOKEN}" \
  -H "X-Tenant-Id: default"
```

**Test Results**:
- ✅ Login creates session in UserSessions table
- ✅ Session ID logged: `57057949-6027-43a8-9ca1-f1789557f32d`
- ✅ Device info captured from User-Agent header
- ✅ IP address captured from HTTP context

### Frontend Testing (Pending)

After implementing frontend changes:

1. **Login from multiple browsers/devices**
   - Chrome desktop
   - Firefox desktop
   - Mobile Safari
   - Mobile Chrome

2. **Navigate to `/sessions`**
   - Verify all sessions are listed
   - Current session should be marked

3. **Test logout from specific device**
   - Click "Logout" on a non-current session
   - Verify session disappears from list
   - Verify that device is actually logged out

4. **Test logout from all devices**
   - Click "Logout From All Devices"
   - Verify redirect to login
   - Verify all other devices are logged out

---

## Security Features

✅ **Token Rotation**: Old refresh token invalidated on every refresh
✅ **Session Expiry**: 7-day absolute expiry (configurable)
✅ **Max Concurrent Sessions**: 5 devices per user (oldest removed automatically)
✅ **Expired Session Cleanup**: Automatic cleanup during token refresh
✅ **BCrypt Hashed**: Refresh tokens are hashed in database
✅ **Tenant Isolation**: Sessions scoped by tenant ID

---

## Configuration

**Session Settings** (`gearify-auth-svc/appsettings.json`):

```json
{
  "Security": {
    "Session": {
      "MaxConcurrentSessions": 5,
      "SessionTimeoutMinutes": 60,
      "RefreshTokenExpiryDays": 7
    }
  }
}
```

---

## Next Steps

1. ✅ Backend implementation - COMPLETE
2. ⏳ **Frontend implementation** - Use this guide
3. ⏳ Test end-to-end flow
4. ⏳ Deploy to staging environment
5. ⏳ User acceptance testing

---

## Files Modified

### Backend
- ✅ `gearify-auth-svc/Application/Commands/LoginCommand.cs` - Added DeviceInfo, IpAddress
- ✅ `gearify-auth-svc/Application/Commands/LoginCommandHandler.cs` - Session creation on login
- ✅ `gearify-auth-svc/Application/Commands/RefreshTokenCommandHandler.cs` - Session validation + rotation
- ✅ `gearify-auth-svc/Application/Commands/LogoutCommand.cs` - NEW: Logout command
- ✅ `gearify-auth-svc/Application/Commands/LogoutCommandHandler.cs` - NEW: Logout implementation
- ✅ `gearify-auth-svc/API/Controllers/AuthController.cs` - Extract device info + IP + Logout endpoint
- ✅ `gearify-auth-svc/Startup.cs` - AWS LocalStack configuration fix
- ✅ `gearify-auth-svc/appsettings.Development.json` - Updated LocalStack endpoint
- ✅ `gearify-api-gateway/appsettings.Development.json` - Updated LocalStack endpoint

### Frontend (Pending)
- ⏳ Create `src/app/components/sessions/sessions.component.ts`
- ⏳ Create `src/app/components/sessions/sessions.component.html`
- ⏳ Create `src/app/components/sessions/sessions.component.scss`
- ⏳ Update `src/app/services/auth.service.ts` - Add session methods
- ⏳ Update `src/app/app-routing.module.ts` - Add sessions route
- ⏳ Update `src/app/components/user-menu/user-menu.component.html` - Add link

---

**Questions?** Check the existing `SessionController.cs` and `SessionService.cs` for implementation details.
