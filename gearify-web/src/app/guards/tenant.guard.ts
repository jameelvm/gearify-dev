import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { map, catchError, of } from 'rxjs';
import { TenantService } from '@core/services/tenant.service';
import { StorageService } from '@core/services/storage.service';

/**
 * Extract tenant ID from subdomain
 */
function extractTenantFromUrl(): string {
  if (typeof window === 'undefined') {
    return 'default';
  }

  const hostname = window.location.hostname;
  const parts = hostname.split('.');

  // Only extract if we have a proper subdomain
  if (parts.length >= 2 && !['localhost', 'www', 'api'].includes(parts[0])) {
    return parts[0];
  }

  // Default fallback for localhost
  if (hostname === 'localhost' || hostname.startsWith('127.0.0.')) {
    return 'default';
  }

  return 'default';
}

/**
 * Guard to validate tenant before allowing route access
 * Only sets tenant in localStorage if validation succeeds
 */
export const tenantGuard: CanActivateFn = (route, state) => {
  const tenantService = inject(TenantService);
  const router = inject(Router);
  const storage = inject(StorageService);

  // Extract tenant from URL
  const tenantId = extractTenantFromUrl();

  // Validate tenant exists
  return tenantService.validateTenant(tenantId).pipe(
    map(isValid => {
      if (!isValid) {
        console.error(`Invalid tenant: ${tenantId}`);
        // Don't set invalid tenant in localStorage
        router.navigate(['/tenant-not-found'], {
          state: {
            tenantId: tenantId,
            reason: 'invalid'
          }
        });
        return false;
      }

      // Tenant is valid - save it to localStorage
      storage.setTenantId(tenantId);
      console.log(`Tenant ${tenantId} validated and saved`);
      return true;
    }),
    catchError(error => {
      console.error('Tenant validation API error:', error);
      // Show error page with API error message
      router.navigate(['/tenant-not-found'], {
        state: {
          tenantId: tenantId,
          reason: 'error',
          errorMessage: error.message || 'Unable to connect to validation service'
        }
      });
      return of(false);
    })
  );
};
