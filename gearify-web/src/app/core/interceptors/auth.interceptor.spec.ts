import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from '../services/auth.service';
import { STORAGE_KEYS } from '@shared/constants/api.constants';

describe('AuthInterceptor', () => {
  let httpMock: HttpTestingController;
  let httpClient: HttpClient;
  let authService: jest.Mocked<AuthService>;

  beforeEach(() => {
    // Create mock for AuthService
    const authServiceMock = {
      getAccessToken: jest.fn(),
      getRefreshToken: jest.fn(),
      refreshToken: jest.fn()
    } as any;

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: authServiceMock }
      ]
    });

    httpMock = TestBed.inject(HttpTestingController);
    httpClient = TestBed.inject(HttpClient);
    authService = TestBed.inject(AuthService) as jest.Mocked<AuthService>;

    // Clear localStorage before each test
    localStorage.clear();
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  describe('Token Headers', () => {
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

    it('should add X-Tenant-Id header when tenant is in localStorage', (done) => {
      authService.getAccessToken.mockReturnValue(null);
      authService.getRefreshToken.mockReturnValue(null);
      localStorage.setItem(STORAGE_KEYS.TENANT_ID, 'test-tenant');

      httpClient.get('/test').subscribe(() => done());

      const req = httpMock.expectOne('/test');
      expect(req.request.headers.get('X-Tenant-Id')).toBe('test-tenant');
      req.flush({});
    });
  });

  describe('Infinite Loop Prevention', () => {
    it('should NOT attempt proactive refresh for /api/auth/refresh endpoint', (done) => {
      // Set up an expiring token scenario
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
  });
});

/**
 * Helper function to create a JWT token that expires in N minutes
 */
function createTokenExpiringIn(minutes: number): string {
  const exp = Math.floor(Date.now() / 1000) + (minutes * 60);
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const payload = btoa(JSON.stringify({ sub: 'test', exp }));
  const signature = 'fake-signature';
  return `${header}.${payload}.${signature}`;
}
