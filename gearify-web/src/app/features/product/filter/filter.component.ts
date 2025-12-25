import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BrandService, Brand } from '@core/services/brand.service';
import { PriceRangeService, PriceRange } from '@core/services/price-range.service';

interface BrandWithCount extends Brand {
  productCount: number;
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

  // Filter dropdowns state
  showBrandDropdown = false;
  showPriceDropdown = false;

  // Brand filter
  brandSearchQuery = '';
  brands = signal<BrandWithCount[]>([]);
  brandsLoading = signal(false);
  brandsError = signal<string | null>(null);

  // Price filter
  priceRanges = signal<PriceRange[]>([]);
  priceRangesLoading = signal(false);
  priceRangesError = signal<string | null>(null);
  customPriceMin = '';
  customPriceMax = '';

  // Sort options
  sortOptions = [
    'Featured Items',
    'Price: Low to High',
    'Price: High to Low',
    'Newest First',
    'Top Rated'
  ];
  selectedSort = 'Featured Items';

  // View mode
  viewMode: 'small' | 'medium' = 'medium';

  ngOnInit() {
    console.log('[FilterComponent] ngOnInit - Loading brands and price ranges...');
    this.loadBrands();
    this.loadPriceRanges();
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

  get filteredBrands() {
    if (!this.brandSearchQuery) return this.brands();
    return this.brands().filter(b =>
      b.name.toLowerCase().includes(this.brandSearchQuery.toLowerCase())
    );
  }
}
