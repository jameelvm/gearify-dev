import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Global error handling interceptor
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        // Unauthorized - logout and redirect to login
        authService.logout();
        router.navigate(['/auth/login']);
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
