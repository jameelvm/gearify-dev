import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap, of, catchError } from 'rxjs';
import { ApiService } from './api.service';
import { User, AuthTokens, LoginRequest, RegisterRequest, AuthState } from '../models/user.model';
import { STORAGE_KEYS, API_CONFIG } from '@shared/constants/api.constants';

/**
 * Authentication Service
 * Handles user authentication, token management, and auth state
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private api = inject(ApiService);
  private router = inject(Router);

  private authState = signal<AuthState>({
    user: this.loadUserFromStorage(),
    tokens: this.loadTokensFromStorage(),
    isAuthenticated: false,
    isLoading: false,
  });

  // Read-only access to auth state
  readonly user = this.authState.asReadonly();
  readonly isAuthenticated = () => !!this.authState().user;

  constructor() {
    // Initialize authentication state
    if (this.authState().tokens) {
      this.authState.update(state => ({ ...state, isAuthenticated: true }));
    }
  }

  /**
   * Log in a user with email and password
   */
  login(credentials: LoginRequest): Observable<{ user: User; token: string; refreshToken: string }> {
    this.authState.update(state => ({ ...state, isLoading: true }));
    return this.api.post<{ user: User; token: string; refreshToken: string }>(API_CONFIG.ENDPOINTS.LOGIN, credentials).pipe(
      tap(response => {
        const tokens = { accessToken: response.token, refreshToken: response.refreshToken, expiresIn: 900 };
        this.setAuthData(response.user, tokens);
        this.authState.update(state => ({
          user: response.user,
          tokens,
          isAuthenticated: true,
          isLoading: false,
        }));
      })
    );
  }

  /**
   * Register a new user
   */
  register(data: RegisterRequest): Observable<{ user: User; token: string; refreshToken: string }> {
    this.authState.update(state => ({ ...state, isLoading: true }));
    const registerData = { ...data, role: data.role || 'Customer' };
    return this.api.post<{ user: User; token: string; refreshToken: string }>(API_CONFIG.ENDPOINTS.REGISTER, registerData).pipe(
      tap(response => {
        const tokens = { accessToken: response.token, refreshToken: response.refreshToken, expiresIn: 900 };
        this.setAuthData(response.user, tokens);
        this.authState.update(state => ({
          user: response.user,
          tokens,
          isAuthenticated: true,
          isLoading: false,
        }));
      })
    );
  }

  /**
   * Log out the current user
   */
  logout(): Observable<void> {
    const refreshToken = this.authState().tokens?.refreshToken;

    // Call backend logout endpoint to revoke session
    if (refreshToken) {
      return this.api.post<{ message: string }>(API_CONFIG.ENDPOINTS.LOGOUT, { refreshToken }).pipe(
        tap(() => {
          console.log('Session revoked successfully');
        }),
        catchError((error) => {
          console.error('Failed to revoke session on server:', error);
          // Return success even if server call fails (fail-safe)
          return of(undefined);
        }),
        tap(() => {
          // Clear localStorage (with SSR check)
          if (typeof localStorage !== 'undefined') {
            localStorage.removeItem(STORAGE_KEYS.ACCESS_TOKEN);
            localStorage.removeItem(STORAGE_KEYS.REFRESH_TOKEN);
            localStorage.removeItem(STORAGE_KEYS.USER);
          }

          // Reset auth state
          this.authState.set({
            user: null,
            tokens: null,
            isAuthenticated: false,
            isLoading: false,
          });

          // Navigate to login page
          this.router.navigate(['/auth/login']);
        })
      );
    } else {
      // No refresh token, just clear local data
      if (typeof localStorage !== 'undefined') {
        localStorage.removeItem(STORAGE_KEYS.ACCESS_TOKEN);
        localStorage.removeItem(STORAGE_KEYS.REFRESH_TOKEN);
        localStorage.removeItem(STORAGE_KEYS.USER);
      }

      this.authState.set({
        user: null,
        tokens: null,
        isAuthenticated: false,
        isLoading: false,
      });

      this.router.navigate(['/auth/login']);

      return of(undefined);
    }
  }

  /**
   * Get the current access token
   */
  getAccessToken(): string | null {
    if (typeof localStorage === 'undefined') return null;
    return localStorage.getItem(STORAGE_KEYS.ACCESS_TOKEN);
  }

  /**
   * Refresh the access token
   */
  refreshToken(): Observable<AuthTokens> {
    const refreshToken = this.authState().tokens?.refreshToken;
    if (!refreshToken) {
      throw new Error('No refresh token available');
    }

    return this.api.post<AuthTokens>(API_CONFIG.ENDPOINTS.REFRESH_TOKEN, { refreshToken }).pipe(
      tap(tokens => {
        const currentUser = this.authState().user;
        if (currentUser) {
          this.setAuthData(currentUser, tokens);
          this.authState.update(state => ({ ...state, tokens }));
        }
      })
    );
  }

  /**
   * Store auth data in localStorage
   */
  private setAuthData(user: User, tokens: AuthTokens): void {
    if (typeof localStorage !== 'undefined') {
      if (tokens.accessToken) {
        localStorage.setItem(STORAGE_KEYS.ACCESS_TOKEN, tokens.accessToken);
      }
      if (tokens.refreshToken) {
        localStorage.setItem(STORAGE_KEYS.REFRESH_TOKEN, tokens.refreshToken);
      }
      localStorage.setItem(STORAGE_KEYS.USER, JSON.stringify(user));
    }
  }

  /**
   * Load user from localStorage
   */
  private loadUserFromStorage(): User | null {
    if (typeof localStorage === 'undefined') return null;
    const userStr = localStorage.getItem(STORAGE_KEYS.USER);
    return userStr ? JSON.parse(userStr) : null;
  }

  /**
   * Load tokens from localStorage
   */
  private loadTokensFromStorage(): AuthTokens | null {
    if (typeof localStorage === 'undefined') return null;
    const accessToken = localStorage.getItem(STORAGE_KEYS.ACCESS_TOKEN);
    const refreshToken = localStorage.getItem(STORAGE_KEYS.REFRESH_TOKEN);

    if (accessToken && refreshToken) {
      return { accessToken, refreshToken, expiresIn: 3600 };
    }
    return null;
  }
}
