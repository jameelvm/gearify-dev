import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { HttpService } from './http.service';
import { API_CONFIG } from '@shared/constants/api.constants';
import { Address, CreateAddressRequest, UpdateAddressRequest } from '@core/models/address.model';

/**
 * Address Service
 * Manages saved delivery addresses for authenticated users
 */
@Injectable({ providedIn: 'root' })
export class AddressService {
  private http = inject(HttpService);

  private addressesSignal = signal<Address[]>([]);
  private loadingSignal = signal(false);

  // Read-only access
  readonly addresses = this.addressesSignal.asReadonly();
  readonly loading = this.loadingSignal.asReadonly();

  /**
   * Get the default address
   */
  get defaultAddress(): Address | null {
    return this.addressesSignal().find(a => a.isDefault) ?? null;
  }

  /**
   * Load all addresses for the current user
   */
  loadAddresses(): Observable<Address[]> {
    this.loadingSignal.set(true);
    return this.http.get<Address[]>(API_CONFIG.ENDPOINTS.ADDRESSES).pipe(
      tap({
        next: (addresses) => {
          this.addressesSignal.set(addresses);
          this.loadingSignal.set(false);
        },
        error: () => {
          this.loadingSignal.set(false);
        }
      })
    );
  }

  /**
   * Create a new address
   */
  createAddress(request: CreateAddressRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(API_CONFIG.ENDPOINTS.ADDRESSES, request).pipe(
      tap(() => {
        // Reload addresses to get the updated list
        this.loadAddresses().subscribe();
      })
    );
  }

  /**
   * Update an existing address
   */
  updateAddress(addressId: string, request: UpdateAddressRequest): Observable<void> {
    return this.http.put<void>(API_CONFIG.ENDPOINTS.ADDRESS_BY_ID(addressId), request).pipe(
      tap(() => {
        // Reload addresses to get the updated list
        this.loadAddresses().subscribe();
      })
    );
  }

  /**
   * Delete an address
   */
  deleteAddress(addressId: string): Observable<void> {
    return this.http.delete<void>(API_CONFIG.ENDPOINTS.ADDRESS_BY_ID(addressId)).pipe(
      tap(() => {
        // Remove from local state
        this.addressesSignal.update(addresses =>
          addresses.filter(a => a.id !== addressId)
        );
      })
    );
  }

  /**
   * Set an address as default
   */
  setDefaultAddress(addressId: string): Observable<void> {
    const address = this.addressesSignal().find(a => a.id === addressId);
    if (!address) {
      throw new Error('Address not found');
    }

    const updateRequest: UpdateAddressRequest = {
      ...address,
      isDefault: true
    };

    return this.updateAddress(addressId, updateRequest);
  }

  /**
   * Clear addresses (e.g., on logout)
   */
  clearAddresses(): void {
    this.addressesSignal.set([]);
  }
}
