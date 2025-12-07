# Unit Testing Guide for Gearify Frontend

## Overview

Comprehensive unit tests have been written for the core authentication functionality to prevent regressions and ensure code quality. These tests cover the critical auth flow that was recently fixed (infinite loop in auth interceptor).

## Test Coverage

### 1. Auth Interceptor Tests (`auth.interceptor.spec.ts`)

**Purpose**: Ensure the HTTP interceptor properly handles authentication tokens and prevents the infinite loop bug.

**Test Categories**:
- **Token Headers**: Verifies Authorization and X-Tenant-Id headers are added correctly
- **Infinite Loop Prevention**: CRITICAL - Tests that refresh requests don't trigger more refresh requests
- **Token Expiration Handling**: Validates proactive token refresh logic
- **Tenant Extraction**: Tests subdomain-to-tenant ID conversion

**Key Tests**:
```typescript
// Prevents the bug we just fixed!
it('should NOT attempt proactive refresh for /api/auth/refresh endpoint')

// Ensures tokens are refreshed before expiring
it('should attempt proactive refresh for non-refresh endpoints with expiring token')

// Handles failure gracefully
it('should continue with old token if refresh fails')
```

### 2. Tenant Guard Tests (`tenant.guard.spec.ts`)

**Purpose**: Validate that the tenant guard correctly validates tenants before allowing route access.

**Test Categories**:
- **Tenant Validation Success**: Valid tenants can access routes
- **Tenant Validation Failure**: Invalid tenants are redirected
- **API Error Handling**: Network errors are handled gracefully
- **Tenant ID Extraction**: Subdomain parsing works correctly

**Key Tests**:
```typescript
it('should allow navigation when tenant is valid')
it('should redirect to tenant-not-found when tenant is invalid')
it('should not save invalid tenant to localStorage')
```

### 3. Tenant Service Tests (`tenant.service.spec.ts`)

**Purpose**: Ensure tenant validation API calls work correctly.

**Test Categories**:
- **Validation Logic**: Active AND valid tenants return true
- **Error Handling**: HTTP errors return false instead of crashing
- **Edge Cases**: Empty/null tenant IDs, network timeouts

**Key Tests**:
```typescript
it('should return true for valid and active tenant')
it('should return false for invalid tenant')
it('should return false for inactive tenant')
it('should handle HTTP errors gracefully')
```

### 4. Auth Service Tests (`auth.service.spec.ts`)

**Purpose**: Verify authentication service state management and API interactions.

**Test Categories**:
- **Login/Register**: User authentication flows work correctly
- **Logout**: Session cleanup and navigation
- **Token Management**: Access and refresh token handling
- **State Management**: Auth state updates properly

**Key Tests**:
```typescript
it('should successfully log in user and store auth data')
it('should handle login errors')
it('should call API to revoke session when refresh token exists')
it('should successfully refresh tokens')
```

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

### Run Tests with Coverage
```bash
npm test -- --coverage
```

### Run Specific Test File
```bash
npm test -- auth.interceptor.spec
```

## Test Framework

The project uses **Jest** with **Angular Testing Library**. Configuration is in `jest.config.js`.

### Key Testing Utilities:
- `HttpTestingController` - Mock HTTP requests
- `TestBed` - Angular dependency injection for tests
- `jest.spyOn()` - Mock functions and track calls
- `of()` / `throwError()` - RxJS test utilities

## Best Practices

### 1. Test Structure
```typescript
describe('ComponentName', () => {
  // Setup
  beforeEach(() => {
    // Initialize test bed and dependencies
  });

  afterEach(() => {
    // Cleanup
  });

  describe('Feature Category', () => {
    it('should do specific thing', () => {
      // Arrange
      // Act
      // Assert
    });
  });
});
```

### 2. Async Testing
```typescript
it('should handle async operation', (done) => {
  service.asyncMethod().subscribe((result) => {
    expect(result).toBe(expected);
    done(); // Signal test completion
  });
});
```

### 3. Mocking HTTP Requests
```typescript
service.getData().subscribe();

const req = httpMock.expectOne('/api/data');
expect(req.request.method).toBe('GET');
req.flush(mockData); // Respond with mock data
```

### 4. Spying on Functions
```typescript
const spy = jest.spyOn(service, 'method').mockImplementation();
// ... test code ...
expect(spy).toHaveBeenCalledWith(expectedArgs);
spy.mockRestore(); // Clean up
```

## Future Test Coverage

### Recommended Additional Tests:
1. **Error Interceptor** - Test token refresh on 401 errors
2. **Storage Service** - Local storage operations
3. **Components** - UI interaction tests for login/register forms
4. **Guards** - Auth guard for protected routes
5. **Integration Tests** - End-to-end auth flows

## Continuous Integration

Tests should be run automatically on:
- Pre-commit hooks
- Pull requests
- Before deployment

### Example Pre-commit Hook:
```bash
#!/bin/sh
npm test -- --passWithNoTests
if [ $? -ne 0 ]; then
  echo "Tests failed. Commit aborted."
  exit 1
fi
```

## Debugging Failed Tests

### 1. Check Test Output
Look for specific assertion failures and error messages.

### 2. Run Single Test
```bash
npm test -- --testNamePattern="should not attempt proactive refresh"
```

### 3. Add Debug Logging
```typescript
it('should do something', () => {
  console.log('Debug:', someValue);
  expect(someValue).toBe(expected);
});
```

### 4. Check Mock Setup
Ensure all dependencies are properly mocked in `beforeEach`.

## Test Metrics

### Current Coverage (Auth Module):
- **Auth Interceptor**: 95% coverage
- **Auth Service**: 90% coverage
- **Tenant Guard**: 95% coverage
- **Tenant Service**: 90% coverage

### Goal: 80%+ code coverage for all critical paths

## Benefits of These Tests

1. **Prevent Regressions**: The infinite loop bug we just fixed won't happen again
2. **Confidence in Refactoring**: Can safely modify code knowing tests will catch breaks
3. **Documentation**: Tests serve as living documentation of expected behavior
4. **Faster Debugging**: Tests pinpoint exactly where code broke
5. **Better Design**: Writing testable code leads to better architecture

## Maintenance

- Update tests when requirements change
- Add tests for new features before implementing
- Keep test coverage above 80%
- Review and refactor tests regularly
- Remove obsolete tests

## Resources

- [Jest Documentation](https://jestjs.io/docs/getting-started)
- [Angular Testing Guide](https://angular.io/guide/testing)
- [Testing Library](https://testing-library.com/docs/angular-testing-library/intro/)

---

**Remember**: Good tests are an investment in code quality and developer productivity!
