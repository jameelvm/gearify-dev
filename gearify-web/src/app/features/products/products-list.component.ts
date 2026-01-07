import { Component, signal, computed, OnInit, OnDestroy, inject, ViewChild, ElementRef, AfterViewInit, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
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
import { ProductService } from '@core/services/product.service';
import { SpecialCollectionsService } from '@core/services/special-collections.service';

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
    FilterComponent
  ],
  templateUrl: './products-list.component.html',
  styleUrl: './products-list.component.scss'
})
export class ProductsListComponent implements OnInit, AfterViewInit, OnDestroy {
  private productService = inject(ProductService);
  private specialCollectionsService = inject(SpecialCollectionsService);
  private route = inject(ActivatedRoute);

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

    this.loadProducts();

    // Listen to route parameter changes (when navigating between subcategories)
    this.route.params.subscribe(() => {
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

    this.loadProducts(false);
  }

  // Sort options
  sortOptions: SelectOption[] = [
    { value: 'newest', label: 'Newest First' },
    { value: 'price-asc', label: 'Price: Low to High' },
    { value: 'price-desc', label: 'Price: High to Low' },
    { value: 'rating', label: 'Highest Rated' },
    { value: 'name', label: 'Name A-Z' }
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
    console.log('Product clicked:', product);
    // Navigate to product detail page
    // this.router.navigate(['/products', product.id]);
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
    this.loadProducts(); // Reset and reload
  }

  /**
   * Toggle filters visibility (for mobile)
   */
  toggleFilters(): void {
    this.showFilters.update(show => !show);
  }
}
