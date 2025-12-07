import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideClientHydration } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors, withFetch } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';

import { routes } from './app.routes';
import { authInterceptor } from '@core/interceptors/auth.interceptor';
import { errorInterceptor } from '@core/interceptors/error.interceptor';
import { STORAGE_KEYS } from '@shared/constants/api.constants';

/**
 * Initialize tenant ID from subdomain (non-blocking)
 */
if (typeof window !== 'undefined') {
  const hostname = window.location.hostname;
  const parts = hostname.split('.');
  let tenantId = 'default'; // Default fallback for localhost

  // Only extract if we have a proper subdomain
  if (parts.length >= 2 && !['localhost', 'www', 'api'].includes(parts[0])) {
    tenantId = parts[0];
  } else if (hostname === 'localhost' || hostname.startsWith('127.0.0.')) {
    tenantId = 'default';
  }

  localStorage.setItem(STORAGE_KEYS.TENANT_ID, tenantId);
  console.log(`Tenant ID set to: ${tenantId}`);
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
    provideAnimations()
  ]
};
