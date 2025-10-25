import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { STORAGE_KEYS } from '@shared/constants/api.constants';

/**
 * HTTP interceptor to inject JWT token and tenant header into requests
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.getAccessToken();

  // Get tenant ID from localStorage or use default
  const tenantId = typeof localStorage !== 'undefined'
    ? localStorage.getItem(STORAGE_KEYS.TENANT_ID) || 'default'
    : 'default';

  const headers: { [key: string]: string } = {
    'X-Tenant-Id': tenantId
  };

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  req = req.clone({
    setHeaders: headers
  });

  return next(req);
};
