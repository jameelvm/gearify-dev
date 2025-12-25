import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '@shared/constants/api.constants';

export interface Brand {
  id: string;
  name: string;
  slug: string;
  productCount?: number;
}

@Injectable({
  providedIn: 'root'
})
export class BrandService {
  private http = inject(HttpClient);
  private apiUrl = API_CONFIG.BASE_URL;

  /**
   * Get all brands
   */
  getBrands(): Observable<Brand[]> {
    console.log('[BrandService] Making API call to:', `${this.apiUrl}${API_CONFIG.ENDPOINTS.BRANDS}`);
    return this.http.get<Brand[]>(`${this.apiUrl}${API_CONFIG.ENDPOINTS.BRANDS}`);
  }

  /**
   * Get brand by ID
   */
  getBrandById(id: string): Observable<Brand> {
    return this.http.get<Brand>(`${this.apiUrl}${API_CONFIG.ENDPOINTS.BRANDS}/${id}`);
  }

  /**
   * Get brand by slug
   */
  getBrandBySlug(slug: string): Observable<Brand> {
    return this.http.get<Brand>(`${this.apiUrl}${API_CONFIG.ENDPOINTS.BRANDS}/slug/${slug}`);
  }
}
