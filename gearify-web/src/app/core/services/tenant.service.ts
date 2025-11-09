import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { environment } from '@environments/environment';

export interface TenantValidationResponse {
  tenantId: string;
  isValid: boolean;
  isActive: boolean;
}

/**
 * Service for validating tenant existence
 */
@Injectable({ providedIn: 'root' })
export class TenantService {
  private http = inject(HttpClient);
  private apiUrl = environment.apiUrl;

  /**
   * Validates if a tenant exists and is active
   * @param tenantId The tenant ID to validate
   * @returns Observable<boolean> - true if valid, false otherwise
   */
  validateTenant(tenantId: string): Observable<boolean> {
    if (!tenantId) {
      return of(false);
    }

    return this.http.get<TenantValidationResponse>(
      `${this.apiUrl}/api/tenants/validate/${tenantId}`
    ).pipe(
      map(response => response.isValid && response.isActive),
      catchError(error => {
        console.error('Tenant validation failed:', error);
        return of(false);
      })
    );
  }

  /**
   * Gets list of available tenants (for development/testing)
   */
  listTenants(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/api/tenants/list`);
  }
}
