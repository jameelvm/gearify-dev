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
  brandSlugs?: string[];  // Support multiple brands
  priceRange?: string | null;
  minPrice?: number;
  maxPrice?: number;
  sortBy?: string;
  collectionId?: string | null;  // Special collection filter (deals, clearance, etc.)
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

    // Support both single brand (from URL) and multiple brands (from filter dropdown)
    if (filters.brandSlug) {
      params = params.set('brandSlug', filters.brandSlug);
    } else if (filters.brandSlugs && filters.brandSlugs.length > 0) {
      // Add multiple brand parameters: ?brand=ss&brand=mrf&brand=kookaburra
      filters.brandSlugs.forEach(brand => {
        params = params.append('brand', brand);
      });
    }

    // Support both price range (from URL) and min/max price (from filter dropdown)
    if (filters.priceRange) {
      params = params.set('priceRange', filters.priceRange);
    } else {
      if (filters.minPrice !== undefined) {
        params = params.set('minPrice', filters.minPrice.toString());
      }
      if (filters.maxPrice !== undefined) {
        params = params.set('maxPrice', filters.maxPrice.toString());
      }
    }

    if (filters.sortBy) {
      params = params.set('sortBy', filters.sortBy);
    }

    if (filters.collectionId) {
      params = params.set('collectionId', filters.collectionId);
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
