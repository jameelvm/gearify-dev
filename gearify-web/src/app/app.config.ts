import { ApplicationConfig, provideZoneChangeDetection, APP_INITIALIZER } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { provideClientHydration } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors, withFetch } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { firstValueFrom } from 'rxjs';

import { routes } from './app.routes';
import { authInterceptor } from '@core/interceptors/auth.interceptor';
import { errorInterceptor } from '@core/interceptors/error.interceptor';
import { TenantService } from '@core/services/tenant.service';
import { STORAGE_KEYS } from '@shared/constants/api.constants';

/**
 * Tenant validation factory function
 * Runs before the app initializes to validate the tenant
 */
function validateTenantFactory(tenantService: TenantService, router: Router) {
  return async () => {
    // Skip validation in SSR
    if (typeof window === 'undefined') {
      return true;
    }

    // Extract tenant from subdomain and store it
    const hostname = window.location.hostname;
    const parts = hostname.split('.');
    let tenantId: string | null = null;

    // Only extract if we have a proper subdomain
    if (parts.length >= 2 && !['localhost', 'www', 'api'].includes(parts[0])) {
      tenantId = parts[0];
    }

    // No tenant found - redirect to error immediately
    if (!tenantId) {
      console.error('No tenant detected in URL. Please use subdomain (e.g., acme.localhost.direct:4200)');
      await router.navigate(['/tenant-not-found']);
      return false;
    }

    localStorage.setItem(STORAGE_KEYS.TENANT_ID, tenantId);

    try {
      console.log(`Validating tenant: ${tenantId}`);
      const isValid = await firstValueFrom(tenantService.validateTenant(tenantId));

      if (!isValid) {
        console.error(`Invalid tenant: ${tenantId}`);
        await router.navigate(['/tenant-not-found']);
        return false;
      }

      console.log(`Tenant ${tenantId} validated successfully`);
      return true;
    } catch (error) {
      console.error('Tenant validation error:', error);
      return true;
    }
  };
}

/**
 * Application configuration
 * Configures routing, SSR hydration, HTTP client, and interceptors
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideClientHydration(),
    provideHttpClient(
      withFetch(),
      withInterceptors([authInterceptor, errorInterceptor])
    ),
    provideAnimations(),
    // Validate tenant before app initialization
    {
      provide: APP_INITIALIZER,
      useFactory: validateTenantFactory,
      deps: [TenantService, Router],
      multi: true
    }
  ]
};
