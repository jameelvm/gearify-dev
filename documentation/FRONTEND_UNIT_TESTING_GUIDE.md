# Frontend Unit Testing Guide - Complete Explanation

> **For developers new to Angular unit testing**
>
> This guide explains every unit test in the Gearify frontend application, what it tests, why it's important, and how it works.

## Table of Contents

1. [What is Unit Testing?](#what-is-unit-testing)
2. [Testing Framework Overview](#testing-framework-overview)
3. [Test File Structure](#test-file-structure)
4. [Auth Interceptor Tests](#auth-interceptor-tests)
5. [Auth Service Tests](#auth-service-tests)
6. [Tenant Service Tests](#tenant-service-tests)
7. [Tenant Guard Tests](#tenant-guard-tests)
8. [Component Tests](#component-tests)
9. [Common Testing Patterns](#common-testing-patterns)
10. [Running Tests](#running-tests)

---

## What is Unit Testing?

**Unit testing** is the practice of testing individual pieces (units) of your code in isolation to ensure they work correctly.

### Why Unit Tests Matter

1. **Catch bugs early** - Find problems before users do
2. **Prevent regressions** - Ensure new code doesn't break existing features
3. **Document behavior** - Tests show how code should work
4. **Refactoring safety** - Change code confidently knowing tests will catch issues

### Example Analogy

Think of unit tests like quality control in a car factory:
- Test each part individually (engine, brakes, lights)
- Before assembling the whole car
- If a part fails, you know exactly which one
- Fix it before it becomes a bigger problem

---

## Testing Framework Overview

### Jest
**Jest** is our testing framework - it runs tests and provides assertion methods.

```typescript
// Example Jest test
it('should add two numbers', () => {
  const result = 2 + 2;
  expect(result).toBe(4); // Assertion
});
```

### Angular Testing Utilities
**TestBed** - Creates an Angular testing environment
**ComponentFixture** - Wrapper around a component for testing
**HttpTestingController** - Mock HTTP requests

---

## Test File Structure

Every test file follows this pattern:

```typescript
describe('ServiceName', () => {           // Test suite - groups related tests
  let service: ServiceName;               // Variable to hold service instance
  let mockDependency: jest.Mocked<Type>;  // Mock dependencies

  beforeEach(() => {                      // Runs before each test
    // Set up test environment
    TestBed.configureTestingModule({
      providers: [/* ... */]
    });
    service = TestBed.inject(ServiceName);
  });

  it('should do something', () => {       // Individual test
    // Arrange - set up test data
    // Act - call the method
    // Assert - check the result
  });
});
```

---

## Auth Interceptor Tests

**File**: `gearify-web/src/app/core/interceptors/auth.interceptor.spec.ts`

### What is an HTTP Interceptor?

An interceptor is middleware that modifies HTTP requests/responses. Think of it like a security checkpoint at an airport - every passenger (HTTP request) goes through it.

### Test Suite Overview

```typescript
describe('AuthInterceptor', () => {
```

This test suite verifies that the auth interceptor properly handles authentication tokens and prevents infinite loops.

---

### Test 1: Add Authorization Header

```typescript
it('should add Authorization header when token is present', (done) => {
  const mockToken = 'test-access-token';
  authService.getAccessToken.mockReturnValue(mockToken);
  authService.getRefreshToken.mockReturnValue(null);
  localStorage.setItem(STORAGE_KEYS.TENANT_ID, 'test-tenant');

  httpClient.get('/test').subscribe(() => done());

  const req = httpMock.expectOne('/test');
  expect(req.request.headers.get('Authorization')).toBe(`Bearer ${mockToken}`);
  req.flush({});
});
```

**What it tests**: The interceptor adds an `Authorization` header with the access token to every HTTP request.

**Why it's important**: Without this header, the backend API won't know who is making the request and will reject it.

**How it works**:
1. **Arrange**: Set up a mock token
2. **Act**: Make an HTTP GET request to `/test`
3. **Assert**: Verify the request has the Authorization header with format `Bearer <token>`

**Real-world example**:
```
Without interceptor: GET /api/users
With interceptor:    GET /api/users
                     Headers: { Authorization: "Bearer abc123..." }
```

---

### Test 2: Add Tenant ID Header

```typescript
it('should add X-Tenant-Id header when tenant is in localStorage', (done) => {
  authService.getAccessToken.mockReturnValue(null);
  authService.getRefreshToken.mockReturnValue(null);
  localStorage.setItem(STORAGE_KEYS.TENANT_ID, 'test-tenant');

  httpClient.get('/test').subscribe(() => done());

  const req = httpMock.expectOne('/test');
  expect(req.request.headers.get('X-Tenant-Id')).toBe('test-tenant');
  req.flush({});
});
```

**What it tests**: The interceptor adds the tenant ID from localStorage to every request.

**Why it's important**: In a multi-tenant system, the backend needs to know which tenant's data to access.

**How it works**:
1. Set tenant ID in localStorage
2. Make a request
3. Verify the `X-Tenant-Id` header is added

---

### Test 3: Prevent Infinite Loop (CRITICAL!)

```typescript
it('should NOT attempt proactive refresh for /api/auth/refresh endpoint', (done) => {
  const expiringToken = createTokenExpiringIn(2); // 2 minutes
  authService.getAccessToken.mockReturnValue(expiringToken);
  authService.getRefreshToken.mockReturnValue('refresh-token');
  localStorage.setItem(STORAGE_KEYS.TENANT_ID, 'test-tenant');

  httpClient.get('http://localhost:8080/api/auth/refresh').subscribe(() => done());

  // Should NOT call refreshToken() for the refresh endpoint itself
  expect(authService.refreshToken).not.toHaveBeenCalled();

  const req = httpMock.expectOne('http://localhost:8080/api/auth/refresh');
  req.flush({});
});
```

**What it tests**: When making a request to refresh the token, the interceptor should NOT try to refresh the token again (preventing infinite loop).

**Why it's important**: This is THE bug that caused the white screen issue! Without this check:
1. User makes request → Token expiring → Interceptor refreshes token
2. Refresh request → Token expiring → Interceptor refreshes token again
3. Refresh request → Token expiring → Interceptor refreshes token again
4. **INFINITE LOOP!** 💥

**How it works**:
1. Create a token that's expiring soon (triggers proactive refresh)
2. Make a request to `/api/auth/refresh`
3. Verify that `refreshToken()` is NOT called (no loop!)

---

## Auth Service Tests

**File**: `gearify-web/src/app/core/services/auth.service.spec.ts`

### What is the Auth Service?

The Auth Service handles user authentication - login, logout, registration, and token management.

---

### Test 1: Service Creation

```typescript
it('should be created', () => {
  expect(service).toBeTruthy();
});
```

**What it tests**: The service can be instantiated without errors.

**Why it's important**: Basic sanity check - if the service can't be created, nothing else will work.

---

### Test 2: Initialize as Not Authenticated

```typescript
it('should initialize as not authenticated when no tokens in storage', () => {
  expect(service.isAuthenticated()).toBe(false);
});
```

**What it tests**: When the app loads and there are no saved tokens, the user should NOT be logged in.

**Why it's important**: Users shouldn't be automatically logged in without credentials.

---

### Test 3: Initialize as Authenticated

```typescript
it('should initialize as authenticated when tokens exist in storage', () => {
  // Reset TestBed with tokens present
  TestBed.resetTestingModule();
  const storageServiceSpy = {
    getUser: jest.fn().mockReturnValue(mockUser),
    getTokens: jest.fn().mockReturnValue(mockTokens),
    // ...
  } as any;

  TestBed.configureTestingModule({/* ... */});
  const newService = TestBed.inject(AuthService);

  expect(newService.isAuthenticated()).toBe(true);
});
```

**What it tests**: If tokens exist in storage (user logged in previously), the service should recognize the user as authenticated.

**Why it's important**: "Remember me" functionality - users stay logged in across browser sessions.

---

### Test 4: Successful Login

```typescript
it('should successfully log in user and store auth data', (done) => {
  const credentials: LoginRequest = {
    email: 'test@test.com',
    password: 'password123'
  };

  const mockResponse = {
    user: mockUser,
    token: 'access-token',
    refreshToken: 'refresh-token'
  };

  apiService.post.mockReturnValue(of(mockResponse));

  service.login(credentials).subscribe(() => {
    expect(apiService.post).toHaveBeenCalled();
    expect(storageService.setAuthData).toHaveBeenCalledWith(
      mockUser,
      expect.objectContaining({
        accessToken: 'access-token',
        refreshToken: 'refresh-token'
      })
    );
    expect(service.isAuthenticated()).toBe(true);
    done();
  });
});
```

**What it tests**: When a user logs in with valid credentials:
1. API is called with email/password
2. User data and tokens are saved to storage
3. User becomes authenticated

**Why it's important**: Core authentication flow - without this working, users can't log in!

**How it works**:
1. **Arrange**: Create login credentials and mock API response
2. **Act**: Call `service.login()`
3. **Assert**: Verify API was called, data was saved, user is authenticated

---

### Test 5: Login Error Handling

```typescript
it('should handle login errors', (done) => {
  const credentials: LoginRequest = {
    email: 'test@test.com',
    password: 'wrong-password'
  };

  const error = new Error('Invalid credentials');
  apiService.post.mockReturnValue(throwError(() => error));

  service.login(credentials).subscribe({
    next: () => fail('Should have failed'),
    error: (err) => {
      expect(err).toBeDefined();
      expect(service.isAuthenticated()).toBe(false);
      done();
    }
  });
});
```

**What it tests**: When login fails (wrong password), the error is properly handled and user remains unauthenticated.

**Why it's important**: Graceful error handling - app shouldn't crash on failed login.

---

### Test 6: Logout with API Call

```typescript
it('should call API to revoke session when refresh token exists', (done) => {
  // Create service with tokens already configured
  TestBed.resetTestingModule();
  const apiServiceSpy = {
    post: jest.fn().mockReturnValue(of({ message: 'Logged out successfully' }))
  } as any;
  // ... configure with tokens

  const authService = TestBed.inject(AuthService);

  authService.logout().subscribe(() => {
    expect(apiServiceSpy.post).toHaveBeenCalled();
    expect(storageServiceSpy.clearAuthData).toHaveBeenCalled();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/auth/login']);
    done();
  });
});
```

**What it tests**: When user logs out:
1. API is called to revoke the session on the server
2. Local auth data is cleared
3. User is redirected to login page

**Why it's important**: Proper logout ensures the session is terminated both client and server-side.

---

### Test 7: Logout with API Error

```typescript
it('should clear auth data even if API call fails', (done) => {
  apiService.post.mockReturnValue(throwError(() => new Error('Network error')));

  authService.logout().subscribe(() => {
    expect(storageServiceSpy.clearAuthData).toHaveBeenCalled();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/auth/login']);
    done();
  });
});
```

**What it tests**: Even if the server is down, logout still clears local data and redirects.

**Why it's important**: Fail-safe behavior - users can always log out even with network issues.

---

### Test 8: Token Refresh

```typescript
it('should successfully refresh tokens', (done) => {
  const newTokens: AuthTokens = {
    accessToken: 'new-access-token',
    refreshToken: 'new-refresh-token',
    expiresIn: 900
  };

  apiServiceSpy.post.mockReturnValue(of(newTokens));

  authService.refreshToken().subscribe((tokens) => {
    expect(tokens).toEqual(newTokens);
    expect(storageServiceSpy.setAuthData).toHaveBeenCalledWith(mockUser, newTokens);
    done();
  });
});
```

**What it tests**: Tokens can be refreshed to extend the user's session.

**Why it's important**: Users stay logged in without having to re-enter password.

---

## Tenant Service Tests

**File**: `gearify-web/src/app/core/services/tenant.service.spec.ts`

### What is the Tenant Service?

The Tenant Service validates tenant IDs to ensure users are accessing a valid organization's data.

---

### Test 1: Valid Tenant

```typescript
it('should return true for valid and active tenant', (done) => {
  const mockResponse: TenantValidationResponse = {
    tenantId: 'test-tenant',
    isValid: true,
    isActive: true
  };

  service.validateTenant('test-tenant').subscribe((result) => {
    expect(result).toBe(true);
    done();
  });

  const req = httpMock.expectOne(`${environment.apiUrl}/api/tenants/validate/test-tenant`);
  req.flush(mockResponse);
});
```

**What it tests**: When a tenant exists and is active, validation returns `true`.

**Why it's important**: Only valid tenants can access the application.

**How it works**:
1. Call `validateTenant('test-tenant')`
2. Mock the HTTP response with valid tenant data
3. Verify the result is `true`

---

### Test 2: Invalid Tenant

```typescript
it('should return false for invalid tenant', (done) => {
  const mockResponse: TenantValidationResponse = {
    tenantId: 'bad-tenant',
    isValid: false,
    isActive: false
  };

  service.validateTenant('bad-tenant').subscribe((result) => {
    expect(result).toBe(false);
    done();
  });

  const req = httpMock.expectOne(`${environment.apiUrl}/api/tenants/validate/bad-tenant`);
  req.flush(mockResponse);
});
```

**What it tests**: Invalid tenants are rejected.

**Why it's important**: Prevent access to non-existent or deactivated organizations.

---

### Test 3: HTTP Error Handling

```typescript
it('should handle HTTP errors gracefully', (done) => {
  const tenantId = 'error-tenant';
  const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation();

  service.validateTenant(tenantId).subscribe((result) => {
    expect(result).toBe(false);
    expect(consoleErrorSpy).toHaveBeenCalled();
    consoleErrorSpy.mockRestore();
    done();
  });

  const req = httpMock.expectOne(`${environment.apiUrl}/api/tenants/validate/${tenantId}`);
  req.error(new ProgressEvent('Network error'), { status: 500, statusText: 'Server Error' });
});
```

**What it tests**: When the API returns an error, the service:
1. Returns `false` (validation failed)
2. Logs the error to console
3. Doesn't crash the application

**Why it's important**: Graceful degradation - app continues working even with API errors.

---

### Test 4: Null/Empty Tenant ID

```typescript
it('should return false for null or empty tenant ID', (done) => {
  service.validateTenant('').subscribe((result) => {
    expect(result).toBe(false);
    done();
  });

  httpMock.expectNone(`${environment.apiUrl}/api/tenants/validate/`);
});
```

**What it tests**: Empty tenant IDs are immediately rejected without calling the API.

**Why it's important**: Performance - don't waste API calls on obviously invalid data.

---

## Tenant Guard Tests

**File**: `gearify-web/src/app/guards/tenant.guard.spec.ts`

### What is a Route Guard?

A route guard is like a security guard at a building entrance - it checks if you're allowed to enter before letting you through.

```typescript
// Without guard:
User clicks "Dashboard" → Dashboard loads immediately

// With guard:
User clicks "Dashboard" → Guard checks tenant → If valid, load Dashboard
                                              → If invalid, redirect to error page
```

---

### Test 1: Allow Navigation for Valid Tenant

```typescript
it('should allow navigation when tenant is valid', (done) => {
  Object.defineProperty(window, 'location', {
    writable: true,
    value: { hostname: 'default.localhost.direct' }
  });

  tenantService.validateTenant.mockReturnValue(of(true));

  TestBed.runInInjectionContext(() => {
    const result = tenantGuard(route, state);

    if (result && typeof result === 'object' && 'subscribe' in result) {
      result.subscribe((canActivate) => {
        expect(canActivate).toBe(true);
        expect(tenantService.validateTenant).toHaveBeenCalledWith('default');
        expect(storageService.setTenantId).toHaveBeenCalledWith('default');
        expect(router.navigate).not.toHaveBeenCalled();
        done();
      });
    }
  });
});
```

**What it tests**: When the tenant is valid:
1. Guard extracts tenant ID from subdomain (`default.localhost.direct` → `default`)
2. Validates the tenant
3. Saves tenant ID to storage
4. Allows navigation (returns `true`)
5. Does NOT redirect

**Why it's important**: Valid users can access the app.

---

### Test 2: Block Navigation for Invalid Tenant

```typescript
it('should redirect to tenant-not-found when tenant is invalid', (done) => {
  Object.defineProperty(window, 'location', {
    writable: true,
    value: { hostname: 'invalid.localhost.direct' }
  });

  tenantService.validateTenant.mockReturnValue(of(false));

  TestBed.runInInjectionContext(() => {
    const result = tenantGuard(route, state);

    if (result && typeof result === 'object' && 'subscribe' in result) {
      result.subscribe((canActivate) => {
        expect(canActivate).toBe(false);
        expect(router.navigate).toHaveBeenCalledWith(['/tenant-not-found'], {
          state: {
            tenantId: 'invalid',
            reason: 'invalid'
          }
        });
        expect(storageService.setTenantId).not.toHaveBeenCalled();
        done();
      });
    }
  });
});
```

**What it tests**: When tenant is invalid:
1. Blocks navigation (returns `false`)
2. Redirects to error page
3. Does NOT save invalid tenant to storage

**Why it's important**: Prevent unauthorized access to other tenants' data.

---

### Test 3: Extract Tenant from Subdomain

```typescript
it('should extract tenant from subdomain correctly', (done) => {
  Object.defineProperty(window, 'location', {
    writable: true,
    value: { hostname: 'mycompany.localhost.direct' }
  });

  tenantService.validateTenant.mockReturnValue(of(true));

  TestBed.runInInjectionContext(() => {
    const result = tenantGuard(route, state);

    if (result && typeof result === 'object' && 'subscribe' in result) {
      result.subscribe(() => {
        expect(tenantService.validateTenant).toHaveBeenCalledWith('mycompany');
        done();
      });
    }
  });
});
```

**What it tests**: The guard correctly extracts tenant ID from the subdomain.

**Real-world examples**:
- `acme.gearify.com` → Tenant: `acme`
- `contoso.gearify.com` → Tenant: `contoso`
- `default.localhost.direct` → Tenant: `default`

**Why it's important**: Multi-tenant apps need to know which organization's data to show.

---

### Test 4: Reserved Subdomains

```typescript
it('should not use reserved subdomains as tenant IDs', (done) => {
  const reservedSubdomains = ['www', 'api', 'localhost'];

  reservedSubdomains.forEach((subdomain) => {
    Object.defineProperty(window, 'location', {
      writable: true,
      value: { hostname: `${subdomain}.example.com` }
    });

    tenantService.validateTenant.mockReturnValue(of(true));

    TestBed.runInInjectionContext(() => {
      const result = tenantGuard(route, state);

      if (result && typeof result === 'object' && 'subscribe' in result) {
        result.subscribe(() => {
          expect(tenantService.validateTenant).toHaveBeenCalledWith('default');
        });
      }
    });
  });

  done();
});
```

**What it tests**: Special subdomains (`www`, `api`, `localhost`) are treated as `default` tenant.

**Why it's important**: Prevent using system subdomains as tenant IDs.

---

## Component Tests

### App Component Tests

**File**: `gearify-web/src/app/app.component.spec.ts`

---

#### Test 1: Component Creation

```typescript
it('should create', () => {
  expect(component).toBeTruthy();
});
```

**What it tests**: The main app component can be created without errors.

**Why it's important**: If the root component fails, the entire app won't load.

---

#### Test 2: Router Outlet Renders

```typescript
it('should render router-outlet', () => {
  fixture.detectChanges();
  const compiled = fixture.nativeElement;
  expect(compiled.querySelector('router-outlet')).toBeTruthy();
});
```

**What it tests**: The component's template includes a `<router-outlet>` element.

**Why it's important**: The router outlet is where page content is displayed. Without it, routing won't work.

---

### Navbar Component Tests

**File**: `gearify-web/src/app/shared/components/navbar/navbar.component.spec.ts`

---

#### Test 1: User Menu Toggle

```typescript
it('should toggle user menu', () => {
  component.showUserMenu = false;
  component.toggleUserMenu();
  expect(component.showUserMenu).toBe(true);

  component.toggleUserMenu();
  expect(component.showUserMenu).toBe(false);
});
```

**What it tests**: Clicking the user menu button toggles the menu open/closed.

**Why it's important**: Basic UI interaction - users can open and close menus.

**How it works**:
1. Start with menu closed (`showUserMenu = false`)
2. Call `toggleUserMenu()` → Menu opens (`showUserMenu = true`)
3. Call again → Menu closes (`showUserMenu = false`)

---

### Login Component Tests

**File**: `gearify-web/src/app/features/auth/login.component.spec.ts`

---

#### Test 1: Email Validation

```typescript
it('should validate email field as required', () => {
  const emailControl = component.loginForm.get('email');
  emailControl?.setValue('');
  expect(emailControl?.hasError('required')).toBe(true);
});
```

**What it tests**: Email field is required - can't submit empty email.

**Why it's important**: Form validation prevents submitting incomplete data.

---

#### Test 2: Email Format Validation

```typescript
it('should validate email format', () => {
  const emailControl = component.loginForm.get('email');
  emailControl?.setValue('invalid-email');
  expect(emailControl?.hasError('email')).toBe(true);
});
```

**What it tests**: Email must be in valid format (e.g., `user@example.com`).

**Why it's important**: Prevent typos and invalid email addresses.

---

#### Test 3: Successful Login Submission

```typescript
it('should successfully submit valid login form', (done) => {
  const mockResponse = {
    user: { id: '1', email: 'test@test.com', /* ... */ },
    token: 'mock-token',
    refreshToken: 'mock-refresh'
  };

  authService.login.mockReturnValue(of(mockResponse));

  component.loginForm.patchValue({
    email: 'test@test.com',
    password: 'password123'
  });

  component.onSubmit();

  expect(authService.login).toHaveBeenCalledWith({
    email: 'test@test.com',
    password: 'password123'
  });

  setTimeout(() => {
    expect(component.isLoading).toBe(false);
    done();
  }, 100);
});
```

**What it tests**: When form is valid and submitted:
1. Loading state is set
2. Auth service is called with credentials
3. Loading state is cleared after response

**Why it's important**: Complete login flow works end-to-end.

---

### Register Component Tests

**File**: `gearify-web/src/app/features/auth/register.component.spec.ts`

---

#### Test 1: Form Initialization

```typescript
it('should initialize with empty form', () => {
  expect(component.registerForm.get('email')?.value).toBe('');
  expect(component.registerForm.get('password')?.value).toBe('');
  expect(component.registerForm.get('firstName')?.value).toBe('');
  expect(component.registerForm.get('lastName')?.value).toBe('');
});
```

**What it tests**: Registration form starts empty.

**Why it's important**: Clean slate for new users.

---

#### Test 2: Required Fields Validation

```typescript
it('should validate required fields', () => {
  const form = component.registerForm;
  expect(form.valid).toBe(false);

  form.patchValue({
    email: 'test@test.com',
    password: 'Password123!',
    confirmPassword: 'Password123!',
    firstName: 'Test',
    lastName: 'User'
  });

  expect(form.valid).toBe(true);
});
```

**What it tests**: Form is invalid when empty, valid when all fields are filled.

**Why it's important**: All required information must be provided to register.

---

## Common Testing Patterns

### Pattern 1: Arrange-Act-Assert (AAA)

Every test follows this structure:

```typescript
it('should do something', () => {
  // ARRANGE - Set up test data
  const input = 'test';
  const expected = 'TEST';

  // ACT - Execute the code being tested
  const result = input.toUpperCase();

  // ASSERT - Verify the result
  expect(result).toBe(expected);
});
```

---

### Pattern 2: Mocking Dependencies

**Mocking** means creating fake versions of services/functions for testing.

```typescript
// Real service makes HTTP calls
const realService = new HttpClient();

// Mock service returns fake data instantly
const mockService = {
  get: jest.fn().mockReturnValue(of({ data: 'fake' }))
};
```

**Why mock?**
- Tests run fast (no real HTTP calls)
- Tests are predictable (always return same fake data)
- Tests don't depend on external services being online

---

### Pattern 3: Asynchronous Tests

When testing async code (HTTP calls, promises), use `done` callback:

```typescript
it('should handle async operation', (done) => {
  service.fetchData().subscribe((result) => {
    expect(result).toBe('success');
    done(); // Tell Jest the test is complete
  });
});
```

---

### Pattern 4: Spying on Functions

**Spying** means watching if a function was called and with what arguments:

```typescript
const spy = jest.spyOn(console, 'log');

someFunction(); // This calls console.log('hello')

expect(spy).toHaveBeenCalled();
expect(spy).toHaveBeenCalledWith('hello');
```

---

## Running Tests

### Run All Tests

```bash
cd gearify-web
npm test
```

### Run Tests in Watch Mode

```bash
npm test -- --watch
```

Watch mode re-runs tests automatically when you save files.

### Run Specific Test File

```bash
npm test -- auth.service.spec
```

### Run Tests with Coverage

```bash
npm test -- --coverage
```

Shows which code is tested and which isn't.

---

## Test Results Explained

```
Test Suites: 8 passed, 8 total
Tests:       69 passed, 69 total
```

- **Test Suites**: Number of `.spec.ts` files (one per component/service)
- **Tests**: Total number of `it()` blocks across all files

---

## Common Testing Terminology

| Term | Meaning | Example |
|------|---------|---------|
| **Unit Test** | Test for a single function/component | Test login function |
| **Integration Test** | Test multiple parts working together | Test login + routing |
| **Mock** | Fake version of a dependency | Fake HTTP service |
| **Spy** | Watch if a function was called | Did logout call API? |
| **Fixture** | Wrapper around component for testing | `ComponentFixture<AppComponent>` |
| **Assertion** | Check if something is true | `expect(x).toBe(5)` |
| **TestBed** | Angular's testing environment | Creates test modules |

---

## Tips for Writing Your Own Tests

### 1. Test One Thing at a Time

```typescript
// ❌ Bad - Tests multiple things
it('should login and redirect', () => {
  // Tests login AND redirect
});

// ✅ Good - Separate tests
it('should successfully log in', () => { /* ... */ });
it('should redirect after login', () => { /* ... */ });
```

### 2. Use Descriptive Test Names

```typescript
// ❌ Bad
it('works', () => { /* ... */ });

// ✅ Good
it('should return false when email is invalid', () => { /* ... */ });
```

### 3. Test Edge Cases

```typescript
it('should handle empty string', () => { /* ... */ });
it('should handle null value', () => { /* ... */ });
it('should handle very long input', () => { /* ... */ });
```

### 4. Don't Test Implementation Details

```typescript
// ❌ Bad - Tests how it works
it('should call private method _validate', () => { /* ... */ });

// ✅ Good - Tests what it does
it('should return true for valid input', () => { /* ... */ });
```

---

## Debugging Failed Tests

When a test fails:

1. **Read the error message**
   ```
   Expected: true
   Received: false
   ```

2. **Check the test name**
   ```
   ✕ should validate email format
   ```

3. **Look at the assertion**
   ```typescript
   expect(emailControl?.hasError('email')).toBe(true);
   //                                            ^^^^
   //                                            Failed here
   ```

4. **Add console.log to debug**
   ```typescript
   console.log('Email value:', emailControl?.value);
   console.log('Errors:', emailControl?.errors);
   ```

---

## Next Steps

Now that you understand the tests:

1. **Run the tests** - See them pass
2. **Make a change** - Break something intentionally
3. **Watch tests fail** - See how they catch the issue
4. **Fix it** - See tests pass again

This cycle is called **Test-Driven Development (TDD)**!

---

## Questions?

Common questions about testing:

**Q: How much should I test?**
A: Test critical functionality first (auth, data access), then add more tests over time.

**Q: Should I test everything?**
A: Focus on business logic and user interactions. Don't test framework code or simple getters/setters.

**Q: How do I know what to test?**
A: Ask: "What could go wrong?" Then write a test for each scenario.

**Q: Tests are slow. Why?**
A: You might be testing with real HTTP calls instead of mocks. Use mocks to speed up tests.

---

## Summary

You now have **69 unit tests** covering:

- ✅ Authentication (login, logout, token refresh)
- ✅ Authorization (interceptor adds headers)
- ✅ Tenant validation (multi-tenancy)
- ✅ Route guards (access control)
- ✅ Components (UI functionality)
- ✅ **Most important**: Infinite loop bug prevention!

**Remember**: Tests are your safety net. They catch bugs before users do! 🎉
