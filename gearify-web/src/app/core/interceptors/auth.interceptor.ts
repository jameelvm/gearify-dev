import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { STORAGE_KEYS } from '@shared/constants/api.constants';

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
 * HTTP interceptor to inject JWT token and tenant header into requests
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

  const headers: { [key: string]: string } = {};

  // Only add tenant header if we have a valid tenant
  if (tenantId) {
    headers['X-Tenant-Id'] = tenantId;
  }

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  req = req.clone({
    setHeaders: headers
  });

  return next(req);
};
