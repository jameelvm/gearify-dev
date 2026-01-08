import { Component, OnInit, inject, signal, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BrandService, Brand } from '@core/services/brand.service';
import { PriceRangeService, PriceRange } from '@core/services/price-range.service';
import { SortOptionsService, SortOption } from '@core/services/sort-options.service';

interface BrandWithCount extends Brand {
  productCount: number;
}

export interface ProductFilters {
  brands: string[];  // Array of brand slugs ['ss', 'mrf', 'kookaburra']
  minPrice?: number;
  maxPrice?: number;
  sortBy?: string;
}

@Component({
  selector: 'app-filter',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './filter.component.html',
  styleUrls: ['./filter.component.scss']
})
export class FilterComponent implements OnInit {
  private brandService = inject(BrandService);
  private priceRangeService = inject(PriceRangeService);
  private sortOptionsService = inject(SortOptionsService);

  // Output event to notify parent component about filter changes
  filtersChanged = output<ProductFilters>();

  // Filter dropdowns state
  showBrandDropdown = false;
  showPriceDropdown = false;

  // Brand filter
  brandSearchQuery = '';
  brands = signal<BrandWithCount[]>([]);
  brandsLoading = signal(false);
  brandsError = signal<string | null>(null);
  selectedBrands = signal<Set<string>>(new Set()); // Track selected brand slugs

  // Price filter
  priceRanges = signal<PriceRange[]>([]);
  priceRangesLoading = signal(false);
  priceRangesError = signal<string | null>(null);
  selectedPriceRange = signal<PriceRange | null>(null); // Single selection for price range
  customPriceMin = '';
  customPriceMax = '';

  // Sort options
  sortOptions = signal<SortOption[]>([]);
  sortOptionsLoading = signal(false);
  sortOptionsError = signal<string | null>(null);
  selectedSort = 'featured'; // Default to featured (will be set from DB)

  // View mode
  viewMode: 'small' | 'medium' = 'medium';

  // Store route params to apply after data loads
  private pendingRouteSync: { brandSlug?: string | null; priceRange?: string | null } | null = null;

  ngOnInit() {
    console.log('[FilterComponent] ngOnInit - Loading brands, price ranges, and sort options...');
    this.loadBrands();
    this.loadPriceRanges();
    this.loadSortOptions();
  }

  loadBrands() {
    console.log('[FilterComponent] loadBrands - Starting to load brands');
    this.brandsLoading.set(true);
    this.brandsError.set(null);

    console.log('[FilterComponent] Making API call to getBrands()');
    this.brandService.getBrands().subscribe({
      next: (brands) => {
        console.log('[FilterComponent] Brands loaded successfully:', brands);
        // Map the API response to include productCount
        const brandsWithCount = brands.map(brand => ({
          ...brand,
          productCount: brand.productCount || 0
        }));
        this.brands.set(brandsWithCount);
        this.brandsLoading.set(false);
        console.log('[FilterComponent] Brands state updated:', this.brands());
      },
      error: (error) => {
        console.error('[FilterComponent] Error loading brands:', error);
        console.error('[FilterComponent] Error details:', {
          message: error.message,
          status: error.status,
          statusText: error.statusText,
          url: error.url
        });
        this.brandsError.set('Failed to load brands');
        this.brandsLoading.set(false);
        // Fallback to empty array or keep existing brands
        this.brands.set([]);
      }
    });
  }

  toggleBrandDropdown() {
    this.showBrandDropdown = !this.showBrandDropdown;
    if (this.showBrandDropdown) {
      this.showPriceDropdown = false;
    }
  }

  togglePriceDropdown() {
    this.showPriceDropdown = !this.showPriceDropdown;
    if (this.showPriceDropdown) {
      this.showBrandDropdown = false;
    }
  }

  closeAllDropdowns() {
    this.showBrandDropdown = false;
    this.showPriceDropdown = false;
  }

  setViewMode(mode: 'small' | 'medium') {
    this.viewMode = mode;
  }

  loadPriceRanges() {
    console.log('[FilterComponent] loadPriceRanges - Starting to load price ranges');
    this.priceRangesLoading.set(true);
    this.priceRangesError.set(null);

    console.log('[FilterComponent] Making API call to getPriceRanges()');
    this.priceRangeService.getPriceRanges().subscribe({
      next: (priceRanges) => {
        console.log('[FilterComponent] Price ranges loaded successfully:', priceRanges);
        this.priceRanges.set(priceRanges);
        this.priceRangesLoading.set(false);
        console.log('[FilterComponent] Price ranges state updated:', this.priceRanges());

        // Apply pending route sync if there is one
        if (this.pendingRouteSync) {
          console.log('[FilterComponent] Applying pending route sync after price ranges loaded');
          this.initializeFromRoute(this.pendingRouteSync.brandSlug, this.pendingRouteSync.priceRange);
          this.pendingRouteSync = null;
        }
      },
      error: (error) => {
        console.error('[FilterComponent] Error loading price ranges:', error);
        console.error('[FilterComponent] Error details:', {
          message: error.message,
          status: error.status,
          statusText: error.statusText,
          url: error.url
        });
        this.priceRangesError.set('Failed to load price ranges');
        this.priceRangesLoading.set(false);
        // Fallback to empty array
        this.priceRanges.set([]);
      }
    });
  }

  loadSortOptions() {
    console.log('[FilterComponent] loadSortOptions - Starting to load sort options');
    this.sortOptionsLoading.set(true);
    this.sortOptionsError.set(null);

    console.log('[FilterComponent] Making API call to getSortOptions()');
    this.sortOptionsService.getSortOptions().subscribe({
      next: (response) => {
        console.log('[FilterComponent] Sort options loaded successfully:', response);
        this.sortOptions.set(response.sortOptions);
        this.sortOptionsLoading.set(false);
        console.log('[FilterComponent] Sort options state updated:', this.sortOptions());
      },
      error: (error) => {
        console.error('[FilterComponent] Error loading sort options:', error);
        console.error('[FilterComponent] Error details:', {
          message: error.message,
          status: error.status,
          statusText: error.statusText,
          url: error.url
        });
        this.sortOptionsError.set('Failed to load sort options');
        this.sortOptionsLoading.set(false);
        // Fallback to empty array
        this.sortOptions.set([]);
      }
    });
  }

  get filteredBrands() {
    if (!this.brandSearchQuery) return this.brands();
    return this.brands().filter(b =>
      b.name.toLowerCase().includes(this.brandSearchQuery.toLowerCase())
    );
  }

  // Brand selection methods
  isBrandSelected(brandSlug: string): boolean {
    return this.selectedBrands().has(brandSlug);
  }

  toggleBrand(brandSlug: string): void {
    const selected = new Set(this.selectedBrands());
    if (selected.has(brandSlug)) {
      selected.delete(brandSlug);
    } else {
      selected.add(brandSlug);
    }
    this.selectedBrands.set(selected);
    this.emitFilters();
  }

  clearBrands(): void {
    this.selectedBrands.set(new Set());
    this.emitFilters();
  }

  /**
   * Clear all filter selections (brands, price range, custom price)
   * Called when route changes to reset dropdown state
   */
  clearAllFilters(): void {
    this.selectedBrands.set(new Set());
    this.selectedPriceRange.set(null);
    this.customPriceMin = '';
    this.customPriceMax = '';
    // Don't emit filters when clearing - parent handles the reload
  }

  /**
   * Initialize filter selections from route parameters
   * Called when route changes to sync dropdown with URL
   */
  initializeFromRoute(brandSlug?: string | null, priceRange?: string | null): void {
    console.log('[FilterComponent] initializeFromRoute called with:', { brandSlug, priceRange });

    // Initialize brand selection from route
    if (brandSlug) {
      this.selectedBrands.set(new Set([brandSlug]));
      console.log('[FilterComponent] Brand synced:', brandSlug);
    } else {
      this.selectedBrands.set(new Set());
    }

    // Initialize price range selection from route
    if (priceRange) {
      // Check if price ranges are loaded yet
      if (this.priceRanges().length === 0 && this.priceRangesLoading()) {
        console.log('[FilterComponent] Price ranges not loaded yet, storing pending sync');
        // Store for later when price ranges load
        this.pendingRouteSync = { brandSlug, priceRange };
        return;
      }

      // Parse route price range (e.g., "0-300")
      const [minStr, maxStr] = priceRange.split('-');
      const min = parseFloat(minStr);
      const max = parseFloat(maxStr);

      console.log('[FilterComponent] Parsed price range:', { min, max });
      console.log('[FilterComponent] Available price ranges:', this.priceRanges());

      // Find matching price range in available ranges
      const matchingRange = this.priceRanges().find(
        pr => {
          const matches = pr.minPrice === min && (pr.maxPrice === max || (max === 999999 && pr.maxPrice === null));
          console.log(`[FilterComponent] Checking range ${pr.label}:`, {
            prMinPrice: pr.minPrice,
            prMaxPrice: pr.maxPrice,
            routeMin: min,
            routeMax: max,
            matches
          });
          return matches;
        }
      );

      if (matchingRange) {
        console.log('[FilterComponent] Price range matched:', matchingRange);
        this.selectedPriceRange.set(matchingRange);
      } else {
        console.log('[FilterComponent] No matching price range found, using custom range');
        // Create custom range if no match found
        this.customPriceMin = minStr;
        this.customPriceMax = maxStr;
        this.selectedPriceRange.set(null);
      }
    } else {
      this.selectedPriceRange.set(null);
      this.customPriceMin = '';
      this.customPriceMax = '';
    }

    console.log('[FilterComponent] Final state:', {
      selectedBrands: Array.from(this.selectedBrands()),
      selectedPriceRange: this.selectedPriceRange()
    });

    // Don't emit filters - parent already has route params and will load products
  }

  // Price range selection methods
  selectPriceRange(priceRange: PriceRange | null): void {
    this.selectedPriceRange.set(priceRange);
    this.emitFilters();
  }

  clearPriceRange(): void {
    this.selectedPriceRange.set(null);
    this.customPriceMin = '';
    this.customPriceMax = '';
    this.emitFilters();
  }

  applyCustomPrice(): void {
    const min = parseFloat(this.customPriceMin);
    const max = parseFloat(this.customPriceMax);

    if (!isNaN(min) && !isNaN(max) && min < max) {
      this.selectedPriceRange.set({
        id: 'custom',
        label: `$${min} - $${max}`,
        minPrice: min,
        maxPrice: max,
        currency: 'USD',
        displayOrder: 999,
        productCount: 0,
        value: `${min}-${max}`
      });
      this.emitFilters();
    }
  }

  // Sort change handler
  onSortChange(sortOption: string): void {
    this.selectedSort = sortOption;
    this.emitFilters();
  }

  // Emit filter changes to parent component
  private emitFilters(): void {
    const selectedRange = this.selectedPriceRange();
    const filters: ProductFilters = {
      brands: Array.from(this.selectedBrands()),
      minPrice: selectedRange?.minPrice,
      maxPrice: selectedRange?.maxPrice ?? undefined,  // Convert null to undefined
      sortBy: this.selectedSort
    };

    console.log('[FilterComponent] Emitting filters:', filters);
    this.filtersChanged.emit(filters);
  }
}
