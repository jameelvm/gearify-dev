import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap, of, catchError, map } from 'rxjs';
import { HttpService } from '@core/services/http.service';
import { StorageService } from '@core/services/storage.service';
import { API_CONFIG } from '@shared/constants/api.constants';
import { User, AuthTokens, LoginRequest, RegisterRequest, AuthState } from '@app/core/models/user.model';

/**
 * Authentication Service
 * Handles user authentication, token management, and auth state
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpService);
  private router = inject(Router);
  private storage = inject(StorageService);

  private authState = signal<AuthState>({
    user: this.storage.getUser(),
    tokens: this.storage.getTokens(),
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
    return this.http.post<{ user: User; token: string; refreshToken: string }>(API_CONFIG.ENDPOINTS.LOGIN, credentials).pipe(
      tap(response => {
        const tokens = { accessToken: response.token, refreshToken: response.refreshToken, expiresIn: 900 };
        this.storage.setAuthData(response.user, tokens);
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
   * Note: Does not auto-login. User must verify email first.
   */
  register(data: RegisterRequest): Observable<{ user: User; token: string; refreshToken: string }> {
    this.authState.update(state => ({ ...state, isLoading: true }));
    const registerData = { ...data, role: data.role || 'Customer' };
    return this.http.post<{ user: User; token: string; refreshToken: string }>(API_CONFIG.ENDPOINTS.REGISTER, registerData).pipe(
      tap(() => {
        // Do NOT store tokens or set authenticated state
        // User must verify email before they can login
        this.authState.update(state => ({
          ...state,
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
      return this.http.post<{ message: string }>(API_CONFIG.ENDPOINTS.LOGOUT, { refreshToken }).pipe(
        catchError((error) => {
          console.error('Failed to revoke session on server:', error);
          // Return empty observable to continue the chain (fail-safe)
          return of({ message: 'Logout failed but continuing' });
        }),
        tap((response) => {
          if (response.message !== 'Logout failed but continuing') {
            console.log('Session revoked successfully');
          }
        }),
        map(() => {
          // Clear auth data from storage
          this.storage.clearAuthData();

          // Reset auth state
          this.authState.set({
            user: null,
            tokens: null,
            isAuthenticated: false,
            isLoading: false,
          });

          // Navigate to login page
          this.router.navigate(['/auth/login']);

          // Return void
          return undefined;
        })
      );
    } else {
      // No refresh token, just clear local data
      this.storage.clearAuthData();

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
    return this.storage.getAccessToken();
  }

  /**
   * Get the current refresh token
   */
  getRefreshToken(): string | null {
    return this.storage.getRefreshToken();
  }

  /**
   * Refresh the access token
   */
  refreshToken(): Observable<AuthTokens> {
    const refreshToken = this.authState().tokens?.refreshToken;
    if (!refreshToken) {
      throw new Error('No refresh token available');
    }

    return this.http.post<AuthTokens>(API_CONFIG.ENDPOINTS.REFRESH_TOKEN, { refreshToken }).pipe(
      tap(tokens => {
        const currentUser = this.authState().user;
        if (currentUser) {
          this.storage.setAuthData(currentUser, tokens);
          this.authState.update(state => ({ ...state, tokens }));
        }
      })
    );
  }

  /**
   * Verify email address using verification token
   */
  verifyEmail(token: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${API_CONFIG.ENDPOINTS.VERIFY_EMAIL}?token=${token}`, {});
  }

  /**
   * Request password reset email
   */
  forgotPassword(email: string): Observable<{ success: boolean; message: string }> {
    return this.http.post<{ success: boolean; message: string }>(API_CONFIG.ENDPOINTS.FORGOT_PASSWORD, { email });
  }

  /**
   * Reset password using reset token
   */
  resetPassword(email: string, resetToken: string, newPassword: string): Observable<{ success: boolean; message: string }> {
    return this.http.post<{ success: boolean; message: string }>(API_CONFIG.ENDPOINTS.RESET_PASSWORD, {
      email,
      resetToken,
      newPassword
    });
  }

  /**
   * Refresh current user data from server
   */
  refreshUser(): Observable<User> {
    return this.http.get<User>(API_CONFIG.ENDPOINTS.ME).pipe(
      tap(user => {
        this.storage.setUser(user);
        this.authState.update(state => ({ ...state, user }));
      })
    );
  }
}
