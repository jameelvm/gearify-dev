import { Injectable, inject } from '@angular/core';
import { HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Product } from '@core/models/product.model';
import { HttpService } from '@core/services/http.service';
import { API_CONFIG } from '@shared/constants/api.constants';

export interface ProductFilters {
  departmentSlug?: string | null;
  categorySlug?: string | null;
  subcategorySlug?: string | null;
  brandSlug?: string | null;
  priceRange?: string | null;
}

export interface ProductListResponse {
  products: Product[];
  total: number;
}

@Injectable({
  providedIn: 'root'
})
export class ProductService {
  private http = inject(HttpService);

  /**
   * Get products by slug-based filters
   */
  getProductsBySlug(filters: ProductFilters): Observable<ProductListResponse> {
    let params = new HttpParams();

    if (filters.departmentSlug) {
      params = params.set('departmentSlug', filters.departmentSlug);
    }
    if (filters.categorySlug) {
      params = params.set('categorySlug', filters.categorySlug);
    }
    if (filters.subcategorySlug) {
      params = params.set('subcategorySlug', filters.subcategorySlug);
    }
    if (filters.brandSlug) {
      params = params.set('brandSlug', filters.brandSlug);
    }
    if (filters.priceRange) {
      params = params.set('priceRange', filters.priceRange);
    }

    return this.http.get<ProductListResponse>(API_CONFIG.ENDPOINTS.PRODUCTS, params);
  }

  /**
   * Get a single product by ID
   */
  getProductById(id: string): Observable<Product> {
    return this.http.get<Product>(API_CONFIG.ENDPOINTS.PRODUCT_BY_ID(id));
  }
}
