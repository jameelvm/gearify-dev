import { Component, signal, computed, OnInit, OnDestroy, inject, ViewChild, ElementRef, AfterViewInit, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Product, ProductFilter } from '@core/models/product.model';
import {
  ProductCardComponent,
  InputComponent,
  SelectComponent,
  ButtonComponent,
  CheckboxComponent
} from '@app/ui-kit/components';
import { SelectOption } from '@app/ui-kit/components/select/select.component';
import { FilterComponent, ProductFilters } from '../product/filter/filter.component';
import { FacetComponent, SearchFacets, FacetFilterEvent } from '@shared/components/facet/facet.component';
import { ProductService } from '@core/services/product.service';
import { SpecialCollectionsService } from '@core/services/special-collections.service';
import { SearchService } from '@core/services/search.service';

export type ViewMode = 'grid' | 'list';
export type SortField = 'price' | 'rating' | 'newest' | 'name';

interface PriceRange {
  min: number;
  max: number;
}

/**
 * Product listing page with filtering, search, sorting, and pagination
 */
@Component({
  selector: 'app-products-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ProductCardComponent,
    InputComponent,
    SelectComponent,
    ButtonComponent,
    CheckboxComponent,
    FilterComponent,
    FacetComponent
  ],
  templateUrl: './products-list.component.html',
  styleUrl: './products-list.component.scss'
})
export class ProductsListComponent implements OnInit, AfterViewInit, OnDestroy {
  private productService = inject(ProductService);
  private specialCollectionsService = inject(SpecialCollectionsService);
  private searchService = inject(SearchService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  // Reference to filter component to clear selections on route change
  @ViewChild(FilterComponent) filterComponent?: FilterComponent;

  // Reference to scroll sentinel for infinite scroll
  @ViewChild('scrollSentinel', { read: ElementRef }) scrollSentinel?: ElementRef;

  // Intersection Observer for infinite scroll
  private observer?: IntersectionObserver;

  // Loading and error states
  isLoading = signal<boolean>(true);
  isLoadingMore = signal<boolean>(false);
  error = signal<string | null>(null);

  // View state
  viewMode = signal<ViewMode>('grid');
  searchQuery = signal<string>('');
  urlSearchQuery = signal<string>(''); // Search query from URL (?q=...)
  isSearchMode = signal<boolean>(false); // True when using search service
  selectedCategory = signal<string>('');
  selectedBrand = signal<string>('');
  priceRange = signal<PriceRange>({ min: 0, max: 10000 });
  sortField = signal<SortField>('newest');

  // Dropdown filters from filter component
  private dropdownFilters = signal<ProductFilters | null>(null);

  // Infinite scroll state
  private nextCursor = signal<string | null>(null);
  hasMore = signal<boolean>(true);  // Public for template access
  private readonly pageSize = 12;

  // Filter visibility (for mobile)
  showFilters = signal<boolean>(true);

  // Products data - accumulated for infinite scroll
  displayedProducts = signal<Product[]>([]);

  // Search facets from search response
  searchFacets = signal<SearchFacets | null>(null);

  // Selected facet filters
  selectedFacetBrands = signal<Set<string>>(new Set());
  selectedFacetCategories = signal<Set<string>>(new Set());
  selectedFacetPriceRange = signal<string | null>(null);
  selectedFacetRating = signal<number | null>(null);

  // Special collection slugs loaded from DB (Set for O(1) lookup)
  private specialCollectionSlugs = new Set<string>();

  constructor() {
    // Effect to observe sentinel after products load (handles *ngIf timing issue)
    effect(() => {
      const products = this.displayedProducts();
      const hasMore = this.hasMore();

      // Only try to observe if we have products AND there are more to load
      if (products.length > 0 && hasMore && this.observer) {
        // Use setTimeout to ensure DOM has updated after *ngIf renders the sentinel
        setTimeout(() => this.tryObserveSentinel(), 0);
      }
    });
  }

  ngOnInit(): void {
    // Load special collections from DB for lookup
    this.loadSpecialCollections();

    // Listen to query parameter changes (for search: ?q=...)
    this.route.queryParams.subscribe(params => {
      const searchQuery = params['q'] || '';
      const brand = params['brand'] || '';
      const category = params['category'] || '';

      this.urlSearchQuery.set(searchQuery);
      this.isSearchMode.set(!!searchQuery || !!brand || !!category);

      // Clear facet selections on new search
      this.clearFacetSelections();

      // If we have search params, load from search service
      if (this.isSearchMode()) {
        this.loadSearchResults(searchQuery, brand, category);
      } else {
        // Clear facets when leaving search mode
        this.searchFacets.set(null);
      }
    });

    // Listen to route parameter changes (when navigating between subcategories)
    this.route.params.subscribe(() => {
      // Skip if in search mode (handled by queryParams)
      if (this.isSearchMode()) return;

      // Clear dropdown filters when route changes (mega menu navigation)
      this.dropdownFilters.set(null);

      // Sync filter component with route parameters (auto-select brand/price from URL)
      if (this.filterComponent) {
        const brandSlug = this.route.snapshot.paramMap.get('brandSlug');
        const range = this.route.snapshot.paramMap.get('range');
        this.filterComponent.initializeFromRoute(brandSlug, range);
      }

      this.loadProducts();
    });

    // Initial load (if not in search mode)
    if (!this.isSearchMode()) {
      this.loadProducts();
    }
  }

  /**
   * Load special collections from database to populate lookup Set
   */
  private loadSpecialCollections(): void {
    // TODO: Get departmentSlug from route or tenant config
    const departmentSlug = this.route.snapshot.paramMap.get('departmentSlug') || 'cricket';

    this.specialCollectionsService.getSpecialCollections(departmentSlug).subscribe({
      next: (response) => {
        // Populate Set with slugs for O(1) lookup
        this.specialCollectionSlugs = new Set(response.collections.map(c => c.slug));
        console.log('[ProductsListComponent] Loaded special collections:', Array.from(this.specialCollectionSlugs));
      },
      error: (err) => {
        console.error('[ProductsListComponent] Failed to load special collections:', err);
        // Continue with empty Set - will treat all categorySlug values as regular categories
      }
    });
  }

  ngAfterViewInit(): void {
    // Set up Intersection Observer for infinite scroll
    this.setupIntersectionObserver();
  }

  ngOnDestroy(): void {
    // Clean up Intersection Observer
    if (this.observer) {
      this.observer.disconnect();
    }
  }

  /**
   * Set up Intersection Observer for infinite scroll
   */
  private setupIntersectionObserver(): void {
    const options = {
      root: null,
      rootMargin: '200px', // Load more when 200px before reaching the sentinel
      threshold: 0.1
    };

    this.observer = new IntersectionObserver((entries) => {
      console.log('[IntersectionObserver] Entries:', entries);
      entries.forEach(entry => {
        console.log('[IntersectionObserver] Entry isIntersecting:', entry.isIntersecting, 'hasMore:', this.hasMore(), 'isLoadingMore:', this.isLoadingMore(), 'isLoading:', this.isLoading());
        if (entry.isIntersecting && this.hasMore() && !this.isLoadingMore() && !this.isLoading()) {
          console.log('[IntersectionObserver] Triggering loadMoreProducts');
          this.loadMoreProducts();
        }
      });
    }, options);

    // Try to observe immediately, but also set up a retry mechanism
    this.tryObserveSentinel();
  }

  /**
   * Try to observe the sentinel element (with retry for dynamic content)
   */
  private tryObserveSentinel(): void {
    if (this.scrollSentinel && this.scrollSentinel.nativeElement) {
      console.log('[IntersectionObserver] Observing sentinel element');
      this.observer?.observe(this.scrollSentinel.nativeElement);
    } else {
      console.log('[IntersectionObserver] Sentinel not found, will retry after products load');
      // Will be called again after products load via effect
    }
  }

  /**
   * Load products from API (first page or reset)
   */
  loadProducts(reset: boolean = true): void {
    if (reset) {
      this.nextCursor.set(null);
      this.displayedProducts.set([]);
      this.hasMore.set(false);  // Reset to false, will be updated by API response
      this.isLoading.set(true);
    } else {
      this.isLoadingMore.set(true);
    }
    this.error.set(null);

    // Read route parameters
    const departmentSlug = this.route.snapshot.paramMap.get('departmentSlug');
    const categorySlug = this.route.snapshot.paramMap.get('categorySlug');
    const subcategorySlug = this.route.snapshot.paramMap.get('subcategorySlug');
    const brandSlug = this.route.snapshot.paramMap.get('brandSlug');
    const range = this.route.snapshot.paramMap.get('range');

    // Get dropdown filters
    const dropdownFilters = this.dropdownFilters();

    // Check if categorySlug is a special collection (not a real category) - O(1) lookup
    const isSpecialCollection = categorySlug && this.specialCollectionSlugs.has(categorySlug);

    // Send both route and dropdown filters - backend will merge/combine them
    const requestFilters = {
      departmentSlug,
      // Only pass categorySlug as filter if:
      // 1. We're drilling down (has subcategory/brand/range), OR
      // 2. It's NOT a special collection (it's a real category)
      categorySlug: (subcategorySlug || brandSlug || range || !isSpecialCollection) ? categorySlug : undefined,
      subcategorySlug,
      // Send route brandSlug (from mega menu navigation)
      brandSlug: brandSlug,
      // Send dropdown brand selections (backend will merge with brandSlug)
      brandSlugs: dropdownFilters?.brands && dropdownFilters.brands.length > 0
        ? dropdownFilters.brands
        : undefined,
      // Dropdown price takes priority, otherwise send route price range
      minPrice: dropdownFilters?.minPrice,
      maxPrice: dropdownFilters?.maxPrice,
      priceRange: (dropdownFilters?.minPrice === undefined && dropdownFilters?.maxPrice === undefined)
        ? range
        : undefined,
      sortBy: dropdownFilters?.sortBy,
      // Pass categorySlug as collectionId - backend will check if it's a valid special collection
      collectionId: categorySlug,
      // Pagination parameters
      pageSize: this.pageSize,
      cursor: reset ? null : this.nextCursor()
    };

    console.log('[ProductsListComponent] Loading products with filters:', requestFilters);

    this.productService.getProductsBySlug(requestFilters).subscribe({
      next: (response) => {
        console.log('[ProductsListComponent] Received response:', response);

        // Update displayed products
        if (reset) {
          this.displayedProducts.set(response.products);
        } else {
          this.displayedProducts.update(products => [...products, ...response.products]);
        }

        // Smart hasMore logic: if we received fewer products than requested, there can't be more
        const receivedLessThanPageSize = response.products.length < this.pageSize;
        const actuallyHasMore = response.hasMore && !receivedLessThanPageSize;

        this.nextCursor.set(response.nextCursor);
        this.hasMore.set(actuallyHasMore);
        this.isLoading.set(false);
        this.isLoadingMore.set(false);

        console.log('[ProductsListComponent] hasMore set to:', actuallyHasMore, 'received:', response.products.length, 'pageSize:', this.pageSize);
      },
      error: (err) => {
        console.error('Error loading products:', err);
        this.error.set('Failed to load products. Please try again later.');
        this.isLoading.set(false);
        this.isLoadingMore.set(false);
      }
    });
  }

  /**
   * Load more products (next page)
   */
  private loadMoreProducts(): void {
    console.log('[ProductsListComponent] loadMoreProducts called, hasMore:', this.hasMore(), 'isLoadingMore:', this.isLoadingMore());
    if (!this.hasMore() || this.isLoadingMore() || this.isLoading()) {
      return;
    }

    if (this.isSearchMode()) {
      this.loadSearchResults(this.urlSearchQuery(), '', '', false);
    } else {
      this.loadProducts(false);
    }
  }

  /**
   * Load search results from Search Service
   */
  private loadSearchResults(query: string, brand: string, category: string, reset: boolean = true): void {
    if (reset) {
      this.displayedProducts.set([]);
      this.hasMore.set(false);
      this.isLoading.set(true);
    } else {
      this.isLoadingMore.set(true);
    }
    this.error.set(null);

    const dropdownFilters = this.dropdownFilters();
    const currentPage = reset ? 1 : Math.floor(this.displayedProducts().length / this.pageSize) + 1;

    // Get facet filters
    const facetBrands = Array.from(this.selectedFacetBrands());
    const facetCategories = Array.from(this.selectedFacetCategories());
    const facetPriceRange = this.selectedFacetPriceRange();
    const facetRating = this.selectedFacetRating();

    // Parse price range from facet selection
    const priceFromFacet = this.parseFacetPriceRange(facetPriceRange);

    console.log('[ProductsListComponent] Search params:', {
      query,
      facetBrands,
      facetCategories,
      facetPriceRange,
      facetRating,
      priceFromFacet
    });

    // Convert brand/category names to slug format for API filtering
    const brandSlug = this.toSlug(facetBrands[0]) || brand || dropdownFilters?.brands?.[0] || undefined;
    const categorySlug = this.toSlug(facetCategories[0]) || category || undefined;

    this.searchService.searchProducts({
      query: query || undefined,
      brand: brandSlug,
      category: categorySlug,
      minPrice: priceFromFacet?.min ?? dropdownFilters?.minPrice,
      maxPrice: priceFromFacet?.max ?? dropdownFilters?.maxPrice,
      minRating: facetRating ?? undefined,
      sortBy: this.mapSortField(dropdownFilters?.sortBy),
      sortOrder: this.mapSortOrder(dropdownFilters?.sortBy),
      page: currentPage,
      pageSize: this.pageSize
    }).subscribe({
      next: (response) => {
        console.log('[ProductsListComponent] Search response:', response);

        // Store facets for sidebar
        if (reset && response.facets) {
          this.searchFacets.set(response.facets);
        }

        // Map search items to Product model
        const products: Product[] = response.items.map(item => ({
          id: item.id,
          tenantId: 'default',
          sku: item.sku,
          name: item.name,
          description: item.description || '',
          department: item.department,
          departmentSlug: item.departmentSlug,
          category: item.category,
          categorySlug: item.categorySlug,
          brand: item.brand,
          brandSlug: item.brandSlug,
          price: item.price,
          compareAtPrice: item.compareAtPrice || item.price,
          discountPercentage: item.discountPercentage,
          currency: item.currency,
          thumbnailUrl: item.thumbnailUrl || item.imageUrl,
          imageUrls: item.imageUrl ? [item.imageUrl] : [],
          tags: [],
          attributes: {},
          isActive: true,
          ratingAverage: item.ratingAverage,
          ratingCount: item.ratingCount,
          createdAt: new Date(),
          updatedAt: new Date()
        }));

        if (reset) {
          this.displayedProducts.set(products);
        } else {
          this.displayedProducts.update(existing => [...existing, ...products]);
        }

        this.hasMore.set(currentPage < response.totalPages);
        this.isLoading.set(false);
        this.isLoadingMore.set(false);
      },
      error: (err) => {
        console.error('Error searching products:', err);
        this.error.set('Failed to search products. Please try again later.');
        this.isLoading.set(false);
        this.isLoadingMore.set(false);
      }
    });
  }

  /**
   * Parse facet price range key to min/max values
   */
  private parseFacetPriceRange(key: string | null): { min?: number; max?: number } | null {
    if (!key) return null;

    switch (key) {
      case 'under_25': return { max: 25 };
      case '25_to_50': return { min: 25, max: 50 };
      case '50_to_100': return { min: 50, max: 100 };
      case '100_to_200': return { min: 100, max: 200 };
      case 'over_200': return { min: 200 };
      default: return null;
    }
  }

  /**
   * Handle facet filter changes from sidebar
   */
  onFacetFilterChange(event: FacetFilterEvent): void {
    console.log('[ProductsListComponent] Facet filter changed:', event);

    switch (event.type) {
      case 'brand':
        this.selectedFacetBrands.update(brands => {
          const updated = new Set(brands);
          if (event.selected) {
            updated.add(event.value);
          } else {
            updated.delete(event.value);
          }
          console.log('[ProductsListComponent] Updated brands:', Array.from(updated));
          return updated;
        });
        break;

      case 'category':
        this.selectedFacetCategories.update(categories => {
          const updated = new Set(categories);
          if (event.selected) {
            updated.add(event.value);
          } else {
            updated.delete(event.value);
          }
          return updated;
        });
        break;

      case 'priceRange':
        this.selectedFacetPriceRange.set(event.selected ? event.value : null);
        break;

      case 'rating':
        const ratingValue = parseInt(event.value.replace('_and_up', ''), 10);
        this.selectedFacetRating.set(event.selected ? ratingValue : null);
        break;
    }

    // Reload search results with new filters
    this.loadSearchResults(this.urlSearchQuery(), '', '');
  }

  /**
   * Clear all facet filter selections
   */
  private clearFacetSelections(): void {
    this.selectedFacetBrands.set(new Set());
    this.selectedFacetCategories.set(new Set());
    this.selectedFacetPriceRange.set(null);
    this.selectedFacetRating.set(null);
  }

  /**
   * Convert display name to slug format
   * "New Balance" -> "new-balance"
   */
  private toSlug(value: string | undefined): string | undefined {
    if (!value) return undefined;
    return value.toLowerCase().replace(/\s+/g, '-');
  }

  /**
   * Map sort field for search API
   */
  private mapSortField(sortBy?: string): 'relevance' | 'price' | 'name' | 'rating' | 'newest' | 'popularity' | undefined {
    if (!sortBy) return 'relevance';
    if (sortBy.startsWith('price')) return 'price';
    if (sortBy.startsWith('name')) return 'name';
    if (sortBy === 'rating') return 'rating';
    if (sortBy === 'newest') return 'newest';
    return 'relevance';
  }

  /**
   * Map sort order for search API
   * Handles both explicit suffixes (-asc/-desc) and implicit defaults
   */
  private mapSortOrder(sortBy?: string): 'asc' | 'desc' | undefined {
    if (!sortBy) return undefined;

    // Explicit suffixes take priority
    if (sortBy.endsWith('-asc')) return 'asc';
    if (sortBy.endsWith('-desc')) return 'desc';

    // Default directions for values without suffix (matching DB sortDirection)
    switch (sortBy) {
      case 'name':
        return 'asc';   // Name A-Z = ascending
      case 'rating':
      case 'newest':
      case 'featured':
        return 'desc';  // These default to descending (highest/newest first)
      default:
        return 'desc';
    }
  }

  // Sort options
  sortOptions: SelectOption[] = [
    { value: 'newest', label: 'Newest First' },
    { value: 'price-asc', label: 'Price: Low to High' },
    { value: 'price-desc', label: 'Price: High to Low' },
    { value: 'rating', label: 'Highest Rated' },
    { value: 'name-asc', label: 'Name A-Z' }
  ];

  /**
   * Toggle view mode between grid and list
   */
  toggleViewMode(): void {
    this.viewMode.update(mode => mode === 'grid' ? 'list' : 'grid');
  }

  /**
   * Set view mode
   */
  setViewMode(mode: ViewMode): void {
    this.viewMode.set(mode);
  }

  /**
   * Handle search input
   */
  onSearch(query: string): void {
    this.searchQuery.set(query);
    this.loadProducts(); // Reset and reload
  }

  /**
   * Handle category filter change
   */
  onCategoryChange(category: string): void {
    this.selectedCategory.set(category);
    this.loadProducts(); // Reset and reload
  }

  /**
   * Handle brand filter change
   */
  onBrandChange(brand: string): void {
    this.selectedBrand.set(brand);
    this.loadProducts(); // Reset and reload
  }

  /**
   * Handle price range change
   */
  onPriceRangeChange(min: number, max: number): void {
    this.priceRange.set({ min, max });
    this.loadProducts(); // Reset and reload
  }

  /**
   * Handle sort change
   */
  onSortChange(value: string): void {
    this.sortField.set(value as SortField);
  }

  /**
   * Handle product card click
   */
  onProductClick(product: Product): void {
    this.router.navigate(['/products', product.id]);
  }

  /**
   * Handle add to cart
   */
  onAddToCart(product: Product): void {
    console.log('Add to cart:', product);
    // Implement cart service call
  }

  /**
   * Handle wishlist toggle
   */
  onToggleWishlist(product: Product): void {
    console.log('Toggle wishlist:', product);
    // Implement wishlist service call
  }

  /**
   * Clear all filters
   */
  clearFilters(): void {
    this.searchQuery.set('');
    this.selectedCategory.set('');
    this.selectedBrand.set('');
    this.priceRange.set({ min: 0, max: 10000 });
    this.dropdownFilters.set(null);

    // Clear filter component UI state (no route params)
    if (this.filterComponent) {
      this.filterComponent.initializeFromRoute(null, null);
    }

    this.loadProducts(); // Reset and reload
  }

  /**
   * Handle filter changes from filter component dropdown
   */
  onFiltersChanged(filters: ProductFilters): void {
    console.log('[ProductsListComponent] Filters changed:', filters);
    this.dropdownFilters.set(filters);

    // Use appropriate loader based on mode
    if (this.isSearchMode()) {
      this.loadSearchResults(this.urlSearchQuery(), '', '');
    } else {
      this.loadProducts();
    }
  }

  /**
   * Toggle filters visibility (for mobile)
   */
  toggleFilters(): void {
    this.showFilters.update(show => !show);
  }
}
