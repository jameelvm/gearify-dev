import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { STORAGE_KEYS } from '@shared/constants/api.constants';
import { switchMap, catchError, of } from 'rxjs';

/**
 * Extracts tenant ID from subdomain
 * Examples:
 * - acme.localhost.direct:4200 -> 'acme'
 * - contoso.localtest.me:4200 -> 'contoso'
 * - localhost:4200 -> null (no tenant, will show error)
 */
function extractTenantFromSubdomain(): string | null {
  if (typeof window === 'undefined') {
    return null;
  }

  const hostname = window.location.hostname;

  // Reserved subdomains that should not be treated as tenants
  const reservedSubdomains = ['www', 'api', 'admin', 'app', 'localhost'];

  // Handle plain localhost or IP addresses - no default fallback!
  if (hostname === 'localhost' || hostname.startsWith('127.0.0.') || hostname.startsWith('192.168.')) {
    return null;
  }

  // Split hostname into parts
  const parts = hostname.split('.');

  // Need at least 2 parts to have a subdomain
  if (parts.length < 2) {
    return null;
  }

  const subdomain = parts[0];

  // Check if it's a reserved subdomain
  if (reservedSubdomains.includes(subdomain.toLowerCase())) {
    return null;
  }

  // Valid tenant subdomain found
  return subdomain;
}

/**
 * Decode JWT token to get expiration time
 */
function getTokenExpiration(token: string): number | null {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    return payload.exp ? payload.exp * 1000 : null; // Convert to milliseconds
  } catch (e) {
    return null;
  }
}

/**
 * Check if token is expired or about to expire (within 5 minutes)
 */
function isTokenExpiringSoon(token: string): boolean {
  const expiration = getTokenExpiration(token);
  if (!expiration) return false;

  const now = Date.now();
  const fiveMinutes = 5 * 60 * 1000; // 5 minutes in milliseconds

  return expiration - now < fiveMinutes;
}

/**
 * HTTP interceptor to inject JWT token and tenant header into requests
 * Also handles proactive token refresh before requests
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.getAccessToken();

  // Priority 1: Check localStorage (for manual override or testing)
  // Priority 2: Extract from subdomain
  // NO DEFAULT FALLBACK - tenant is required
  let tenantId: string | null;

  if (typeof localStorage !== 'undefined') {
    const storedTenant = localStorage.getItem(STORAGE_KEYS.TENANT_ID);
    if (storedTenant) {
      tenantId = storedTenant;
    } else {
      // Extract from subdomain and store it
      tenantId = extractTenantFromSubdomain();
      if (tenantId) {
        localStorage.setItem(STORAGE_KEYS.TENANT_ID, tenantId);
      }
    }
  } else {
    tenantId = extractTenantFromSubdomain();
  }

  // Skip proactive refresh for the refresh endpoint itself to avoid infinite loop
  const isRefreshRequest = req.url.includes('/api/auth/refresh');

  // Check if token needs refresh before making the request
  // Only attempt refresh if we have a refresh token available
  const refreshToken = authService.getRefreshToken();

  if (token && refreshToken && isTokenExpiringSoon(token) && !isRefreshRequest) {
    // Token is expired or about to expire, refresh it first
    return authService.refreshToken().pipe(
      switchMap((tokens) => {
        const newToken = tokens.accessToken || tokens.token;
        return next(addAuthHeaders(req, newToken || token, tenantId));
      }),
      catchError((error) => {
        // If refresh fails, continue with existing token and let error interceptor handle it
        console.warn('Proactive token refresh failed, continuing with existing token:', error);
        return next(addAuthHeaders(req, token, tenantId));
      })
    );
  }

  // Token is still valid or no refresh token available, proceed normally
  return next(addAuthHeaders(req, token, tenantId));
};

/**
 * Add authentication and tenant headers to request
 */
function addAuthHeaders(req: any, token: string | null, tenantId: string | null): any {
  const headers: { [key: string]: string } = {};

  // Only add tenant header if we have a valid tenant
  if (tenantId) {
    headers['X-Tenant-Id'] = tenantId;
  }

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  return req.clone({
    setHeaders: headers
  });
}
