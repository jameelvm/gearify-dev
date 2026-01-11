import { Injectable, inject } from '@angular/core';
import { Observable, Subject, of } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, catchError, tap, map, filter } from 'rxjs/operators';
import { HttpService } from './http.service';
import { API_CONFIG } from '@shared/constants/api.constants';

// ============================================================================
// DTOs - Response types from Search Service
// ============================================================================

export interface AutocompleteSuggestion {
  text: string;
  type: 'brand' | 'category' | 'product';
  id?: string;
  slug?: string;
}

export interface AutocompleteResponse {
  suggestions: AutocompleteSuggestion[];
}

export interface ProductSearchItem {
  id: string;
  sku: string;
  name: string;
  description?: string;
  brand: string;
  brandSlug: string;
  department: string;
  departmentSlug: string;
  category: string;
  categorySlug: string;
  price: number;
  compareAtPrice?: number;
  discountPercentage?: number;
  currency: string;
  imageUrl?: string;
  thumbnailUrl?: string;
  ratingAverage?: number;
  ratingCount?: number;
  isDeal: boolean;
  isClearance: boolean;
  isNewArrival: boolean;
  isBestSeller: boolean;
}

export interface FacetItem {
  key: string;
  count: number;
}

export interface SearchFacets {
  brands?: FacetItem[];
  categories?: FacetItem[];
  departments?: FacetItem[];
  priceRanges?: FacetItem[];
  ratings?: FacetItem[];
}

export interface SearchProductsResponse {
  items: ProductSearchItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  facets?: SearchFacets;
}

export interface SearchProductsParams {
  query?: string;
  category?: string;
  brand?: string;
  department?: string;
  minPrice?: number;
  maxPrice?: number;
  minRating?: number;
  tags?: string[];
  dealsOnly?: boolean;
  clearanceOnly?: boolean;
  newArrivalsOnly?: boolean;
  bestSellersOnly?: boolean;
  sortBy?: 'relevance' | 'price' | 'name' | 'rating' | 'newest' | 'popularity';
  sortOrder?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

// ============================================================================
// Search Service - Handles all search operations with RxJS
// ============================================================================

@Injectable({
  providedIn: 'root'
})
export class SearchService {
  private http = inject(HttpService);

  // Subject for autocomplete input - allows reactive streaming
  private autocompleteInput$ = new Subject<string>();

  // Minimum characters required before triggering autocomplete
  private readonly MIN_AUTOCOMPLETE_LENGTH = 2;

  // Debounce time in milliseconds for autocomplete
  private readonly DEBOUNCE_TIME = 300;

  /**
   * Creates an observable stream for autocomplete suggestions.
   * Uses RxJS operators for optimal UX:
   * - debounceTime: Waits for user to stop typing
   * - distinctUntilChanged: Only emits when value actually changes
   * - switchMap: Cancels previous requests when new input arrives
   * - catchError: Gracefully handles errors
   */
  getAutocompleteStream(): Observable<AutocompleteSuggestion[]> {
    return this.autocompleteInput$.pipe(
      debounceTime(this.DEBOUNCE_TIME),
      distinctUntilChanged(),
      filter(prefix => prefix.length >= this.MIN_AUTOCOMPLETE_LENGTH),
      switchMap(prefix => this.fetchAutocompleteSuggestions(prefix)),
      catchError(() => of([]))
    );
  }

  /**
   * Pushes a new search term to the autocomplete stream.
   * Call this from the component on input changes.
   */
  updateAutocompleteInput(prefix: string): void {
    this.autocompleteInput$.next(prefix);
  }

  /**
   * Clears autocomplete by pushing empty string.
   */
  clearAutocomplete(): void {
    this.autocompleteInput$.next('');
  }

  /**
   * Fetches autocomplete suggestions from the search service.
   * Returns empty array for short prefixes.
   */
  fetchAutocompleteSuggestions(prefix: string, limit: number = 10): Observable<AutocompleteSuggestion[]> {
    if (!prefix || prefix.length < this.MIN_AUTOCOMPLETE_LENGTH) {
      return of([]);
    }

    const params = new URLSearchParams();
    params.set('prefix', prefix);
    params.set('limit', limit.toString());

    return this.http.get<AutocompleteResponse>(
      `${API_CONFIG.ENDPOINTS.SEARCH_AUTOCOMPLETE}?${params.toString()}`
    ).pipe(
      map(response => response.suggestions || []),
      catchError(() => of([]))
    );
  }

  /**
   * Searches for products with various filters and sorting options.
   */
  searchProducts(params: SearchProductsParams): Observable<SearchProductsResponse> {
    const queryParams = new URLSearchParams();

    if (params.query) queryParams.set('q', params.query);
    if (params.category) queryParams.set('category', params.category);
    if (params.brand) queryParams.set('brand', params.brand);
    if (params.department) queryParams.set('department', params.department);
    if (params.minPrice !== undefined) queryParams.set('minPrice', params.minPrice.toString());
    if (params.maxPrice !== undefined) queryParams.set('maxPrice', params.maxPrice.toString());
    if (params.minRating !== undefined) queryParams.set('minRating', params.minRating.toString());
    if (params.tags?.length) queryParams.set('tags', params.tags.join(','));
    if (params.dealsOnly) queryParams.set('dealsOnly', 'true');
    if (params.clearanceOnly) queryParams.set('clearanceOnly', 'true');
    if (params.newArrivalsOnly) queryParams.set('newArrivalsOnly', 'true');
    if (params.bestSellersOnly) queryParams.set('bestSellersOnly', 'true');
    if (params.sortBy) queryParams.set('sortBy', params.sortBy);
    if (params.sortOrder) queryParams.set('sortOrder', params.sortOrder);
    if (params.page !== undefined) queryParams.set('page', params.page.toString());
    if (params.pageSize !== undefined) queryParams.set('pageSize', params.pageSize.toString());

    const queryString = queryParams.toString();
    const endpoint = queryString
      ? `${API_CONFIG.ENDPOINTS.SEARCH_PRODUCTS}?${queryString}`
      : API_CONFIG.ENDPOINTS.SEARCH_PRODUCTS;

    return this.http.get<SearchProductsResponse>(endpoint).pipe(
      catchError(() => of({
        items: [],
        totalCount: 0,
        page: params.page || 1,
        pageSize: params.pageSize || 20,
        totalPages: 0
      }))
    );
  }

  /**
   * Quick search by query string only - convenience method.
   */
  quickSearch(query: string, pageSize: number = 20): Observable<SearchProductsResponse> {
    return this.searchProducts({ query, pageSize });
  }

  /**
   * Get similar/recommended products for a given product.
   * Uses brand, category, price range, and tags for matching.
   */
  getSimilarProducts(productId: string, limit: number = 4): Observable<SimilarProductsResponse> {
    const endpoint = `${API_CONFIG.ENDPOINTS.SEARCH_SIMILAR(productId)}?limit=${limit}`;

    return this.http.get<SimilarProductsResponse>(endpoint).pipe(
      catchError(() => of({
        items: [],
        productId,
        matchStrategy: 'error'
      }))
    );
  }
}

export interface SimilarProductsResponse {
  items: ProductSearchItem[];
  productId: string;
  matchStrategy?: string;
}
