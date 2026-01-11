import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

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

export interface FacetFilterEvent {
  type: 'brand' | 'category' | 'department' | 'priceRange' | 'rating';
  value: string;
  selected: boolean;
}

@Component({
  selector: 'app-facet',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './facet.component.html',
  styleUrls: ['./facet.component.scss']
})
export class FacetComponent {
  // Input facets from search response
  facets = input<SearchFacets | null>(null);

  // Currently applied filters
  selectedBrands = input<Set<string>>(new Set());
  selectedCategories = input<Set<string>>(new Set());
  selectedPriceRange = input<string | null>(null);
  selectedRating = input<number | null>(null);

  // Output filter change events
  filterChange = output<FacetFilterEvent>();

  // Collapsed state for each section
  collapsedSections: Record<string, boolean> = {};

  toggleSection(section: string): void {
    this.collapsedSections[section] = !this.collapsedSections[section];
  }

  isCollapsed(section: string): boolean {
    return this.collapsedSections[section] ?? false;
  }

  onBrandClick(brand: FacetItem): void {
    const isSelected = this.selectedBrands().has(brand.key);
    this.filterChange.emit({
      type: 'brand',
      value: brand.key,
      selected: !isSelected
    });
  }

  onCategoryClick(category: FacetItem): void {
    const isSelected = this.selectedCategories().has(category.key);
    this.filterChange.emit({
      type: 'category',
      value: category.key,
      selected: !isSelected
    });
  }

  onPriceRangeClick(priceRange: FacetItem): void {
    const isSelected = this.selectedPriceRange() === priceRange.key;
    this.filterChange.emit({
      type: 'priceRange',
      value: priceRange.key,
      selected: !isSelected
    });
  }

  onRatingClick(rating: FacetItem): void {
    const ratingValue = this.parseRatingKey(rating.key);
    const isSelected = this.selectedRating() === ratingValue;
    this.filterChange.emit({
      type: 'rating',
      value: rating.key,
      selected: !isSelected
    });
  }

  isBrandSelected(brand: FacetItem): boolean {
    return this.selectedBrands().has(brand.key);
  }

  isCategorySelected(category: FacetItem): boolean {
    return this.selectedCategories().has(category.key);
  }

  isPriceRangeSelected(priceRange: FacetItem): boolean {
    return this.selectedPriceRange() === priceRange.key;
  }

  isRatingSelected(rating: FacetItem): boolean {
    return this.selectedRating() === this.parseRatingKey(rating.key);
  }

  formatPriceRange(key: string): string {
    switch (key) {
      case 'under_25': return 'Under $25';
      case '25_to_50': return '$25 to $50';
      case '50_to_100': return '$50 to $100';
      case '100_to_200': return '$100 to $200';
      case 'over_200': return '$200 & Above';
      default: return key;
    }
  }

  formatRating(key: string): number {
    return this.parseRatingKey(key);
  }

  /**
   * Get ratings sorted by stars descending (4 stars first, then 3, 2, 1)
   * Only includes ratings with count > 0
   */
  getSortedRatings(): FacetItem[] {
    const ratings = this.facets()?.ratings;
    if (!ratings) return [];

    return [...ratings]
      .filter(r => r.count > 0)
      .sort((a, b) => this.parseRatingKey(b.key) - this.parseRatingKey(a.key));
  }

  private parseRatingKey(key: string): number {
    // Parse keys like "4_and_up" -> 4
    const match = key.match(/^(\d+)_and_up$/);
    return match ? parseInt(match[1], 10) : 0;
  }
}
