import { HttpInterceptorFn, HttpErrorResponse, HttpRequest, HttpHandlerFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError, switchMap, BehaviorSubject, filter, take } from 'rxjs';
import { API_CONFIG } from '@shared/constants/api.constants';
import { AuthService } from '@app/features/auth';

let isRefreshing = false;
let refreshTokenSubject: BehaviorSubject<string | null> = new BehaviorSubject<string | null>(null);

/**
 * Global error handling interceptor with automatic token refresh
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        // Check if this is the refresh token endpoint itself failing
        if (req.url.includes(API_CONFIG.ENDPOINTS.REFRESH_TOKEN)) {
          // Refresh token failed - clear tokens immediately and logout
          // Clear tokens synchronously to prevent further refresh attempts
          if (typeof localStorage !== 'undefined') {
            localStorage.removeItem('access_token');
            localStorage.removeItem('refresh_token');
            localStorage.removeItem('user');
          }

          // Navigate to login immediately
          router.navigate(['/auth/login']);

          // Trigger logout to clean up (but don't wait for it)
          authService.logout().subscribe({
            error: (err) => console.error('Logout error:', err)
          });

          return throwError(() => error);
        }

        // Attempt to refresh the token
        return handle401Error(req, next, authService, router);
      } else if (error.status === 403) {
        // Forbidden - redirect to access denied
        router.navigate(['/access-denied']);
      } else if (error.status === 404) {
        console.error('Resource not found:', error.url);
      } else if (error.status >= 500) {
        console.error('Server error:', error.message);
      }

      return throwError(() => error);
    })
  );
};

/**
 * Handle 401 errors by attempting to refresh the token
 */
function handle401Error(
  request: HttpRequest<any>,
  next: HttpHandlerFn,
  authService: AuthService,
  router: Router
) {
  if (!isRefreshing) {
    isRefreshing = true;
    refreshTokenSubject.next(null);

    return authService.refreshToken().pipe(
      switchMap((tokens) => {
        isRefreshing = false;

        const accessToken = tokens.accessToken || tokens.token;
        if (!accessToken) {
          throw new Error('No access token in refresh response');
        }

        refreshTokenSubject.next(accessToken);

        // Retry the failed request with the new token
        return next(addTokenToRequest(request, accessToken));
      }),
      catchError((err) => {
        isRefreshing = false;

        // Refresh failed - clear tokens immediately
        if (typeof localStorage !== 'undefined') {
          localStorage.removeItem('access_token');
          localStorage.removeItem('refresh_token');
          localStorage.removeItem('user');
        }

        // Navigate to login immediately
        router.navigate(['/auth/login']);

        // Trigger logout to clean up (but don't wait for it)
        authService.logout().subscribe({
          error: (logoutErr) => console.error('Logout error:', logoutErr)
        });

        return throwError(() => err);
      })
    );
  } else {
    // Wait for the token to be refreshed, then retry the request
    return refreshTokenSubject.pipe(
      filter(token => token !== null),
      take(1),
      switchMap(token => {
        return next(addTokenToRequest(request, token!));
      })
    );
  }
}

/**
 * Add the access token to the request headers
 */
function addTokenToRequest(request: HttpRequest<any>, token: string): HttpRequest<any> {
  return request.clone({
    setHeaders: {
      Authorization: `Bearer ${token}`
    }
  });
}
