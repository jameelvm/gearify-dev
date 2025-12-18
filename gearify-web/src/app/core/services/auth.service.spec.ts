import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { ApiService } from './api.service';
import { StorageService } from './storage.service';
import { User, AuthTokens, LoginRequest, RegisterRequest } from '../models/user.model';

describe('AuthService', () => {
  let service: AuthService;
  let apiService: jest.Mocked<ApiService>;
  let storageService: jest.Mocked<StorageService>;
  let router: jest.Mocked<Router>;

  const mockUser: User = {
    id: '123',
    email: 'test@example.com',
    firstName: 'Test',
    lastName: 'User',
    role: 'Customer',
    tenantId: 'default',
    isActive: true
  };

  const mockTokens: AuthTokens = {
    accessToken: 'mock-access-token',
    refreshToken: 'mock-refresh-token',
    expiresIn: 900
  };

  beforeEach(() => {
    const apiServiceSpy = {
      post: jest.fn()
    } as any;
    const storageServiceSpy = {
      getUser: jest.fn(),
      getTokens: jest.fn(),
      getAccessToken: jest.fn(),
      getRefreshToken: jest.fn(),
      setAuthData: jest.fn(),
      clearAuthData: jest.fn()
    } as any;
    const routerSpy = {
      navigate: jest.fn()
    } as any;

    TestBed.configureTestingModule({
      providers: [
        AuthService,
        { provide: ApiService, useValue: apiServiceSpy },
        { provide: StorageService, useValue: storageServiceSpy },
        { provide: Router, useValue: routerSpy }
      ]
    });

    apiService = TestBed.inject(ApiService) as jest.Mocked<ApiService>;
    storageService = TestBed.inject(StorageService) as jest.Mocked<StorageService>;
    router = TestBed.inject(Router) as jest.Mocked<Router>;

    // Reset storage mocks
    storageService.getUser.mockReturnValue(null);
    storageService.getTokens.mockReturnValue(null);
    storageService.getAccessToken.mockReturnValue(null);
    storageService.getRefreshToken.mockReturnValue(null);

    service = TestBed.inject(AuthService);
  });

  describe('Initialization', () => {
    it('should be created', () => {
      expect(service).toBeTruthy();
    });

    it('should initialize as not authenticated when no tokens in storage', () => {
      expect(service.isAuthenticated()).toBe(false);
    });

    it('should initialize as authenticated when tokens exist in storage', () => {
      // Need to reset and reconfigure TestBed with tokens present before service is created
      TestBed.resetTestingModule();
      const apiServiceSpy = {
        post: jest.fn()
      } as any;
      const storageServiceSpy = {
        getUser: jest.fn().mockReturnValue(mockUser),
        getTokens: jest.fn().mockReturnValue(mockTokens),
        getAccessToken: jest.fn(),
        getRefreshToken: jest.fn(),
        setAuthData: jest.fn(),
        clearAuthData: jest.fn()
      } as any;
      const routerSpy = {
        navigate: jest.fn()
      } as any;

      TestBed.configureTestingModule({
        providers: [
          AuthService,
          { provide: ApiService, useValue: apiServiceSpy },
          { provide: StorageService, useValue: storageServiceSpy },
          { provide: Router, useValue: routerSpy }
        ]
      });

      const newService = TestBed.inject(AuthService);
      expect(newService.isAuthenticated()).toBe(true);
    });
  });

  describe('login', () => {
    it('should successfully log in user and store auth data', (done) => {
      const credentials: LoginRequest = {
        email: 'test@example.com',
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

    it('should update auth state on successful login', (done) => {
      const credentials: LoginRequest = {
        email: 'test@example.com',
        password: 'password123'
      };

      const mockResponse = {
        user: mockUser,
        token: 'access-token',
        refreshToken: 'refresh-token'
      };

      apiService.post.mockReturnValue(of(mockResponse));

      service.login(credentials).subscribe(() => {
        expect(service.user().user).toEqual(mockUser);
        expect(service.isAuthenticated()).toBe(true);
        done();
      });
    });

    it('should handle login errors', (done) => {
      const credentials: LoginRequest = {
        email: 'test@example.com',
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
  });

  describe('register', () => {
    it('should successfully register user without storing auth data (email verification required)', (done) => {
      const registerData: RegisterRequest = {
        email: 'newuser@example.com',
        password: 'password123',
        firstName: 'New',
        lastName: 'User'
      };

      const mockResponse = {
        user: { ...mockUser, email: 'newuser@example.com' },
        token: 'access-token',
        refreshToken: 'refresh-token'
      };

      apiService.post.mockReturnValue(of(mockResponse));

      service.register(registerData).subscribe(() => {
        expect(apiService.post).toHaveBeenCalled();
        // Auth data should NOT be stored - user must verify email first
        expect(storageService.setAuthData).not.toHaveBeenCalled();
        // User should NOT be authenticated until email is verified
        expect(service.isAuthenticated()).toBe(false);
        done();
      });
    });

    it('should add default Customer role if not provided', (done) => {
      const registerData: RegisterRequest = {
        email: 'newuser@example.com',
        password: 'password123',
        firstName: 'New',
        lastName: 'User'
      };

      const mockResponse = {
        user: mockUser,
        token: 'access-token',
        refreshToken: 'refresh-token'
      };

      apiService.post.mockReturnValue(of(mockResponse));

      service.register(registerData).subscribe(() => {
        expect(apiService.post).toHaveBeenCalledWith(
          expect.any(String),
          expect.objectContaining({ role: 'Customer' })
        );
        done();
      });
    });

    it('should preserve provided role', (done) => {
      const registerData: RegisterRequest = {
        email: 'admin@example.com',
        password: 'password123',
        firstName: 'Admin',
        lastName: 'User',
        role: 'Admin'
      };

      const mockResponse = {
        user: { ...mockUser, role: 'Admin' },
        token: 'access-token',
        refreshToken: 'refresh-token'
      };

      apiService.post.mockReturnValue(of(mockResponse));

      service.register(registerData).subscribe(() => {
        expect(apiService.post).toHaveBeenCalledWith(
          expect.any(String),
          expect.objectContaining({ role: 'Admin' })
        );
        done();
      });
    });

    it('should handle registration errors', (done) => {
      const registerData: RegisterRequest = {
        email: 'existing@example.com',
        password: 'password123',
        firstName: 'Test',
        lastName: 'User'
      };

      const error = new Error('Email already exists');
      apiService.post.mockReturnValue(throwError(() => error));

      service.register(registerData).subscribe({
        next: () => fail('Should have failed'),
        error: (err) => {
          expect(err).toBeDefined();
          expect(service.isAuthenticated()).toBe(false);
          done();
        }
      });
    });
  });

  describe('logout', () => {
    it('should call API to revoke session when refresh token exists', (done) => {
      // Create service with tokens already in auth state
      TestBed.resetTestingModule();
      const apiServiceSpy = {
        post: jest.fn().mockReturnValue(of({ message: 'Logged out successfully' }))
      } as any;
      const storageServiceSpy = {
        getUser: jest.fn().mockReturnValue(mockUser),
        getTokens: jest.fn().mockReturnValue(mockTokens),
        getAccessToken: jest.fn(),
        getRefreshToken: jest.fn(),
        setAuthData: jest.fn(),
        clearAuthData: jest.fn()
      } as any;
      const routerSpy = {
        navigate: jest.fn()
      } as any;

      TestBed.configureTestingModule({
        providers: [
          AuthService,
          { provide: ApiService, useValue: apiServiceSpy },
          { provide: StorageService, useValue: storageServiceSpy },
          { provide: Router, useValue: routerSpy }
        ]
      });

      const authService = TestBed.inject(AuthService);

      authService.logout().subscribe(() => {
        expect(apiServiceSpy.post).toHaveBeenCalled();
        expect(storageServiceSpy.clearAuthData).toHaveBeenCalled();
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/auth/login']);
        done();
      });
    });

    it('should clear auth data even if API call fails', (done) => {
      // Create service with tokens
      TestBed.resetTestingModule();
      const apiServiceSpy = {
        post: jest.fn().mockReturnValue(throwError(() => new Error('Network error')))
      } as any;
      const storageServiceSpy = {
        getUser: jest.fn().mockReturnValue(mockUser),
        getTokens: jest.fn().mockReturnValue(mockTokens),
        getAccessToken: jest.fn(),
        getRefreshToken: jest.fn(),
        setAuthData: jest.fn(),
        clearAuthData: jest.fn()
      } as any;
      const routerSpy = {
        navigate: jest.fn()
      } as any;

      TestBed.configureTestingModule({
        providers: [
          AuthService,
          { provide: ApiService, useValue: apiServiceSpy },
          { provide: StorageService, useValue: storageServiceSpy },
          { provide: Router, useValue: routerSpy }
        ]
      });

      const authService = TestBed.inject(AuthService);

      authService.logout().subscribe(() => {
        // Even if API call fails, logout should clear data and navigate
        expect(storageServiceSpy.clearAuthData).toHaveBeenCalled();
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/auth/login']);
        done();
      });
    });

    it('should clear local data when no refresh token exists', (done) => {
      // Service already initialized with no tokens (from main beforeEach)
      service.logout().subscribe(() => {
        expect(apiService.post).not.toHaveBeenCalled();
        expect(storageService.clearAuthData).toHaveBeenCalled();
        expect(router.navigate).toHaveBeenCalledWith(['/auth/login']);
        done();
      });
    });

    it('should reset auth state after logout', (done) => {
      // Create service with tokens
      TestBed.resetTestingModule();
      const apiServiceSpy = {
        post: jest.fn().mockReturnValue(of({ message: 'Logged out' }))
      } as any;
      const storageServiceSpy = {
        getUser: jest.fn().mockReturnValue(mockUser),
        getTokens: jest.fn().mockReturnValue(mockTokens),
        getAccessToken: jest.fn(),
        getRefreshToken: jest.fn(),
        setAuthData: jest.fn(),
        clearAuthData: jest.fn()
      } as any;
      const routerSpy = {
        navigate: jest.fn()
      } as any;

      TestBed.configureTestingModule({
        providers: [
          AuthService,
          { provide: ApiService, useValue: apiServiceSpy },
          { provide: StorageService, useValue: storageServiceSpy },
          { provide: Router, useValue: routerSpy }
        ]
      });

      const authService = TestBed.inject(AuthService);

      authService.logout().subscribe(() => {
        expect(authService.user().user).toBeNull();
        expect(authService.user().tokens).toBeNull();
        expect(authService.isAuthenticated()).toBe(false);
        done();
      });
    });
  });

  describe('getAccessToken', () => {
    it('should return access token from storage', () => {
      storageService.getAccessToken.mockReturnValue('test-token');
      expect(service.getAccessToken()).toBe('test-token');
      expect(storageService.getAccessToken).toHaveBeenCalled();
    });

    it('should return null when no token exists', () => {
      storageService.getAccessToken.mockReturnValue(null);
      expect(service.getAccessToken()).toBeNull();
    });
  });

  describe('getRefreshToken', () => {
    it('should return refresh token from storage', () => {
      storageService.getRefreshToken.mockReturnValue('refresh-token');
      expect(service.getRefreshToken()).toBe('refresh-token');
      expect(storageService.getRefreshToken).toHaveBeenCalled();
    });

    it('should return null when no refresh token exists', () => {
      storageService.getRefreshToken.mockReturnValue(null);
      expect(service.getRefreshToken()).toBeNull();
    });
  });

  describe('refreshToken', () => {
    it('should throw error when no refresh token available', () => {
      // Service already initialized with no tokens (from main beforeEach)
      expect(() => service.refreshToken()).toThrowError('No refresh token available');
    });

    it('should handle refresh token errors', (done) => {
      // Create a service with tokens by reconfiguring
      TestBed.resetTestingModule();
      const apiServiceSpy = {
        post: jest.fn()
      } as any;
      const storageServiceSpy = {
        getUser: jest.fn().mockReturnValue(mockUser),
        getTokens: jest.fn().mockReturnValue(mockTokens),
        getAccessToken: jest.fn().mockReturnValue(mockTokens.accessToken),
        getRefreshToken: jest.fn().mockReturnValue(mockTokens.refreshToken),
        setAuthData: jest.fn(),
        clearAuthData: jest.fn()
      } as any;
      const routerSpy = {
        navigate: jest.fn()
      } as any;

      TestBed.configureTestingModule({
        providers: [
          AuthService,
          { provide: ApiService, useValue: apiServiceSpy },
          { provide: StorageService, useValue: storageServiceSpy },
          { provide: Router, useValue: routerSpy }
        ]
      });

      const authService = TestBed.inject(AuthService);
      const error = new Error('Invalid refresh token');
      apiServiceSpy.post.mockReturnValue(throwError(() => error));

      authService.refreshToken().subscribe({
        next: () => fail('Should have failed'),
        error: (err) => {
          expect(err).toBeDefined();
          done();
        }
      });
    });

    it('should successfully refresh tokens', (done) => {
      const newTokens: AuthTokens = {
        accessToken: 'new-access-token',
        refreshToken: 'new-refresh-token',
        expiresIn: 900
      };

      // Create a service with tokens by reconfiguring
      TestBed.resetTestingModule();
      const apiServiceSpy = {
        post: jest.fn().mockReturnValue(of(newTokens))
      } as any;
      const storageServiceSpy = {
        getUser: jest.fn().mockReturnValue(mockUser),
        getTokens: jest.fn().mockReturnValue(mockTokens),
        getAccessToken: jest.fn().mockReturnValue(mockTokens.accessToken),
        getRefreshToken: jest.fn().mockReturnValue(mockTokens.refreshToken),
        setAuthData: jest.fn(),
        clearAuthData: jest.fn()
      } as any;
      const routerSpy = {
        navigate: jest.fn()
      } as any;

      TestBed.configureTestingModule({
        providers: [
          AuthService,
          { provide: ApiService, useValue: apiServiceSpy },
          { provide: StorageService, useValue: storageServiceSpy },
          { provide: Router, useValue: routerSpy }
        ]
      });

      const authService = TestBed.inject(AuthService);

      authService.refreshToken().subscribe((tokens) => {
        expect(tokens).toEqual(newTokens);
        expect(storageServiceSpy.setAuthData).toHaveBeenCalledWith(mockUser, newTokens);
        done();
      });
    });

    it('should not update storage if no current user', (done) => {
      const newTokens: AuthTokens = {
        accessToken: 'new-access-token',
        refreshToken: 'new-refresh-token',
        expiresIn: 900
      };

      // Create a service with tokens but no user
      TestBed.resetTestingModule();
      const apiServiceSpy = {
        post: jest.fn().mockReturnValue(of(newTokens))
      } as any;
      const storageServiceSpy = {
        getUser: jest.fn().mockReturnValue(null),
        getTokens: jest.fn().mockReturnValue(mockTokens),
        getAccessToken: jest.fn().mockReturnValue(mockTokens.accessToken),
        getRefreshToken: jest.fn().mockReturnValue(mockTokens.refreshToken),
        setAuthData: jest.fn(),
        clearAuthData: jest.fn()
      } as any;
      const routerSpy = {
        navigate: jest.fn()
      } as any;

      TestBed.configureTestingModule({
        providers: [
          AuthService,
          { provide: ApiService, useValue: apiServiceSpy },
          { provide: StorageService, useValue: storageServiceSpy },
          { provide: Router, useValue: routerSpy }
        ]
      });

      const authService = TestBed.inject(AuthService);

      authService.refreshToken().subscribe(() => {
        expect(storageServiceSpy.setAuthData).not.toHaveBeenCalled();
        done();
      });
    });
  });
});
