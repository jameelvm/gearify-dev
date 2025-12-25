import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '@shared/constants/api.constants';

export interface PriceRange {
  id: string;
  label: string;
  minPrice: number;
  maxPrice: number | null;
  currency: string;
  displayOrder: number;
  category?: string;
  productCount: number;
  value: string;
}

@Injectable({
  providedIn: 'root'
})
export class PriceRangeService {
  private http = inject(HttpClient);
  private apiUrl = API_CONFIG.BASE_URL;

  /**
   * Get all price ranges for filtering
   * @param category Optional category to filter price ranges
   */
  getPriceRanges(category?: string): Observable<PriceRange[]> {
    console.log('[PriceRangeService] Making API call to:', `${this.apiUrl}${API_CONFIG.ENDPOINTS.PRICE_RANGES}`);

    const url = category
      ? `${this.apiUrl}${API_CONFIG.ENDPOINTS.PRICE_RANGES}?category=${category}`
      : `${this.apiUrl}${API_CONFIG.ENDPOINTS.PRICE_RANGES}`;

    return this.http.get<PriceRange[]>(url);
  }
}
