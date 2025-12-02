# Logout Implementation Example

## Backend Endpoint

**URL**: `POST /api/auth/logout`
**Auth**: Required (Bearer Token)

**Request**:
```json
{
  "refreshToken": "stqgGuNr6HxbdI68yr9wucJnYwT6p0JLHGnZw0BbYfVDXlDdVSFg17cKL06/nKDj25IawVAc+/ESz8mss8kiaA=="
}
```

**Response (Success)**:
```json
{
  "message": "Logged out successfully"
}
```

**Response (Error)**:
```json
{
  "error": "Session not found"
}
```

---

## Frontend Implementation

### 1. Auth Service Method

```typescript
// src/app/services/auth.service.ts

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(
    private http: HttpClient,
    private router: Router
  ) {}

  /**
   * Logout from current session
   * Revokes the session on the server and clears local storage
   */
  async logout(): Promise<void> {
    const refreshToken = localStorage.getItem('refresh_token');

    if (refreshToken) {
      try {
        // Call logout endpoint to revoke the session
        await this.http.post('/api/auth/logout', {
          refreshToken
        }).toPromise();

        console.log('Session revoked successfully');
      } catch (error) {
        // Even if logout fails on server, still clear local tokens
        console.error('Server logout failed, clearing local tokens anyway', error);
      }
    }

    // Clear tokens from local storage
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');

    // Redirect to login page
    this.router.navigate(['/login']);
  }
}
```

### 2. Component Usage

```typescript
// src/app/components/header/header.component.ts

@Component({
  selector: 'app-header',
  templateUrl: './header.component.html'
})
export class HeaderComponent {
  constructor(private authService: AuthService) {}

  async onLogout() {
    if (confirm('Are you sure you want to logout?')) {
      await this.authService.logout();
    }
  }
}
```

### 3. Template

```html
<!-- src/app/components/header/header.component.html -->

<mat-toolbar color="primary">
  <span>Gearify</span>

  <span class="spacer"></span>

  <button mat-icon-button [matMenuTriggerFor]="userMenu">
    <mat-icon>account_circle</mat-icon>
  </button>

  <mat-menu #userMenu="matMenu">
    <button mat-menu-item routerLink="/profile">
      <mat-icon>person</mat-icon>
      <span>Profile</span>
    </button>

    <button mat-menu-item routerLink="/sessions">
      <mat-icon>devices</mat-icon>
      <span>Active Sessions</span>
    </button>

    <mat-divider></mat-divider>

    <!-- Logout Button -->
    <button mat-menu-item (click)="onLogout()">
      <mat-icon>logout</mat-icon>
      <span>Logout</span>
    </button>
  </mat-menu>
</mat-toolbar>
```

---

## How It Works

### Backend Flow:

1. **Extract User ID** from JWT token (from Authorization header)
2. **Find Session** using the refresh token from request body
3. **Validate Session** - check if it exists and is active
4. **Revoke Session** - set `IsActive = false` in UserSessions table
5. **Return Success** message

**Code** (`LogoutCommandHandler.cs`):
```csharp
public async Task<LogoutResult> Handle(LogoutCommand request, CancellationToken cancellationToken)
{
    // Find the session by refresh token
    var session = await _sessionService.ValidateSessionAsync(request.UserId, request.RefreshToken);

    if (session == null)
    {
        return new LogoutResult(false, "Session not found");
    }

    // Revoke the session
    var revoked = await _sessionService.RevokeSessionAsync(request.UserId, session.Id);

    if (revoked)
    {
        _logger.LogInformation("User {UserId} logged out from session {SessionId}", request.UserId, session.Id);
        return new LogoutResult(true, "Logged out successfully");
    }

    return new LogoutResult(false, "Failed to revoke session");
}
```

### Frontend Flow:

1. **Get refresh token** from localStorage
2. **Call `/api/auth/logout`** with refresh token in request body
3. **Clear tokens** from localStorage (even if server call fails)
4. **Redirect** to login page

---

## Testing

### Manual Test (cURL):

```bash
# 1. Login first to get tokens
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "User-Agent: Mozilla/5.0" \
  --data @test-login.json

# Save the tokens from response:
# ACCESS_TOKEN="eyJhbGci..."
# REFRESH_TOKEN="stqgGuNr..."

# 2. Logout
curl -X POST http://localhost:8080/api/auth/logout \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -d "{\"refreshToken\": \"$REFRESH_TOKEN\"}"

# Expected response:
# {"message":"Logged out successfully"}

# 3. Try to use the refresh token again (should fail)
curl -X POST http://localhost:8080/api/auth/refresh \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -d "{\"refreshToken\": \"$REFRESH_TOKEN\"}"

# Expected response:
# {"error":"Invalid or expired session"}
```

### Frontend Test:

1. **Login** to the application
2. Open **Browser DevTools** > Application > Local Storage
3. Verify `access_token` and `refresh_token` are stored
4. Click **Logout** button in the UI
5. Verify:
   - ✅ Redirected to login page
   - ✅ Tokens removed from localStorage
   - ✅ Can't access protected routes anymore

---

## Important Notes

### Security Considerations:

1. **Always send refresh token** to revoke the session - this ensures the specific device/session is logged out
2. **Clear tokens even if logout fails** - prevents user from thinking they're still logged in
3. **Use HTTPS in production** - refresh tokens are sensitive
4. **Short access token expiry** (15 min) minimizes risk if token is stolen

### Error Handling:

The logout method is designed to be **fail-safe**:
- If server logout fails, tokens are still cleared locally
- User is always redirected to login
- This prevents the user from being stuck in a "logged in" state

### Multi-Device Support:

When user logs out:
- ✅ Current session is revoked
- ✅ Other devices remain logged in
- ✅ User can view/manage all sessions from "Active Sessions" page

To logout from **all devices**:
```typescript
await this.authService.logoutAllSessions();
```

---

## Complete Example

Here's a complete working example you can copy/paste:

**auth.service.ts**:
```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly API_URL = '/api/auth';

  constructor(
    private http: HttpClient,
    private router: Router
  ) {}

  async logout(): Promise<void> {
    const refreshToken = localStorage.getItem('refresh_token');

    if (refreshToken) {
      try {
        await this.http.post(`${this.API_URL}/logout`, {
          refreshToken
        }).toPromise();
      } catch (error) {
        console.error('Logout failed on server', error);
      }
    }

    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');
    this.router.navigate(['/login']);
  }

  isAuthenticated(): boolean {
    return !!localStorage.getItem('access_token');
  }
}
```

**header.component.ts**:
```typescript
import { Component } from '@angular/core';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-header',
  templateUrl: './header.component.html'
})
export class HeaderComponent {
  constructor(public authService: AuthService) {}

  async onLogout() {
    await this.authService.logout();
  }
}
```

**header.component.html**:
```html
<button mat-menu-item (click)="onLogout()">
  <mat-icon>logout</mat-icon>
  <span>Logout</span>
</button>
```

That's it! Your logout functionality is now complete with proper session revocation. 🎉
