import { Component, signal, computed, OnInit, inject, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Product, ProductFilter } from '@core/models/product.model';
import {
  ProductCardComponent,
  PaginationComponent,
  InputComponent,
  SelectComponent,
  ButtonComponent,
  CheckboxComponent
} from '@app/ui-kit/components';
import { PageChangeEvent } from '@app/ui-kit/components/pagination/pagination.component';
import { SelectOption } from '@app/ui-kit/components/select/select.component';
import { FilterComponent, ProductFilters } from '../product/filter/filter.component';
import { ProductService } from '@core/services/product.service';

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
    PaginationComponent,
    InputComponent,
    SelectComponent,
    ButtonComponent,
    CheckboxComponent,
    FilterComponent
  ],
  templateUrl: './products-list.component.html',
  styleUrl: './products-list.component.scss'
})
export class ProductsListComponent implements OnInit {
  private productService = inject(ProductService);
  private route = inject(ActivatedRoute);

  // Reference to filter component to clear selections on route change
  @ViewChild(FilterComponent) filterComponent?: FilterComponent;

  // Loading and error states
  isLoading = signal<boolean>(true);
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

  // Pagination state
  currentPage = signal<number>(1);
  itemsPerPage = signal<number>(12);

  // Filter visibility (for mobile)
  showFilters = signal<boolean>(true);

  // Products data
  private allProducts = signal<Product[]>([]);

  ngOnInit(): void {
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
   * Load products from API
   */
  loadProducts(): void {
    this.isLoading.set(true);
    this.error.set(null);

    // Read route parameters
    const departmentSlug = this.route.snapshot.paramMap.get('departmentSlug');
    const categorySlug = this.route.snapshot.paramMap.get('categorySlug');
    const subcategorySlug = this.route.snapshot.paramMap.get('subcategorySlug');
    const brandSlug = this.route.snapshot.paramMap.get('brandSlug');
    const range = this.route.snapshot.paramMap.get('range');

    // Get dropdown filters
    const dropdownFilters = this.dropdownFilters();

    // Send both route and dropdown filters - backend will merge/combine them
    const requestFilters = {
      departmentSlug,
      categorySlug,
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
      sortBy: dropdownFilters?.sortBy
    };

    console.log('[ProductsListComponent] Loading products with filters:', requestFilters);

    this.productService.getProductsBySlug(requestFilters).subscribe({
      next: (response) => {
        this.allProducts.set(response.products);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Error loading products:', err);
        this.error.set('Failed to load products. Please try again later.');
        this.isLoading.set(false);
      }
    });
  }

  // Available filter options
  categories = computed(() => {
    const cats = new Set(this.allProducts().map(p => p.category));
    return Array.from(cats).sort();
  });

  brands = computed(() => {
    const brds = new Set(this.allProducts().map(p => p.brand));
    return Array.from(brds).sort();
  });

  // Sort options
  sortOptions: SelectOption[] = [
    { value: 'newest', label: 'Newest First' },
    { value: 'price-asc', label: 'Price: Low to High' },
    { value: 'price-desc', label: 'Price: High to Low' },
    { value: 'rating', label: 'Highest Rated' },
    { value: 'name', label: 'Name A-Z' }
  ];

  // Filtered and sorted products
  filteredProducts = computed(() => {
    let products = this.allProducts();

    // Apply search filter
    const search = this.searchQuery().toLowerCase();
    if (search) {
      products = products.filter(p =>
        p.name.toLowerCase().includes(search) ||
        p.description.toLowerCase().includes(search) ||
        p.brand.toLowerCase().includes(search) ||
        p.category.toLowerCase().includes(search) ||
        p.tags.some(tag => tag.toLowerCase().includes(search))
      );
    }

    // Apply category filter
    const category = this.selectedCategory();
    if (category) {
      products = products.filter(p => p.category === category);
    }

    // Apply brand filter
    const brand = this.selectedBrand();
    if (brand) {
      products = products.filter(p => p.brand === brand);
    }

    // Apply price range filter
    const range = this.priceRange();
    products = products.filter(p => p.price >= range.min && p.price <= range.max);

    // Apply sorting
    const sort = this.sortField();
    products = [...products].sort((a, b) => {
      switch (sort) {
        case 'price':
          return a.price - b.price;
        case 'rating':
          return (b.rating?.average || 0) - (a.rating?.average || 0);
        case 'newest':
          return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
        case 'name':
          return a.name.localeCompare(b.name);
        default:
          return 0;
      }
    });

    return products;
  });

  // Paginated products
  paginatedProducts = computed(() => {
    const products = this.filteredProducts();
    const page = this.currentPage();
    const perPage = this.itemsPerPage();
    const start = (page - 1) * perPage;
    const end = start + perPage;
    return products.slice(start, end);
  });

  // Total pages
  totalPages = computed(() => {
    return Math.ceil(this.filteredProducts().length / this.itemsPerPage());
  });

  // Total items
  totalItems = computed(() => this.filteredProducts().length);

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
    this.currentPage.set(1); // Reset to first page on search
  }

  /**
   * Handle category filter change
   */
  onCategoryChange(category: string): void {
    this.selectedCategory.set(category);
    this.currentPage.set(1);
  }

  /**
   * Handle brand filter change
   */
  onBrandChange(brand: string): void {
    this.selectedBrand.set(brand);
    this.currentPage.set(1);
  }

  /**
   * Handle price range change
   */
  onPriceRangeChange(min: number, max: number): void {
    this.priceRange.set({ min, max });
    this.currentPage.set(1);
  }

  /**
   * Handle sort change
   */
  onSortChange(value: string): void {
    this.sortField.set(value as SortField);
  }

  /**
   * Handle page change
   */
  onPageChange(event: PageChangeEvent): void {
    this.currentPage.set(event.page);
    this.itemsPerPage.set(event.itemsPerPage);
    // Scroll to top on page change
    window.scrollTo({ top: 0, behavior: 'smooth' });
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
    this.currentPage.set(1);
    this.dropdownFilters.set(null);

    // Clear filter component UI state (no route params)
    if (this.filterComponent) {
      this.filterComponent.initializeFromRoute(null, null);
    }

    this.loadProducts();
  }

  /**
   * Handle filter changes from filter component dropdown
   */
  onFiltersChanged(filters: ProductFilters): void {
    console.log('[ProductsListComponent] Filters changed:', filters);
    this.dropdownFilters.set(filters);
    this.currentPage.set(1); // Reset to first page when filters change
    this.loadProducts();
  }

  /**
   * Toggle filters visibility (for mobile)
   */
  toggleFilters(): void {
    this.showFilters.update(show => !show);
  }
}
