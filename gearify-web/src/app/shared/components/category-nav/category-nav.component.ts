import { Component, signal, OnInit, OnDestroy, inject, ElementRef, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { CategoryService } from '@core/services/category.service';
import { SpecialCollectionsService, SpecialCollectionDto } from '@core/services/special-collections.service';
import { SearchService, AutocompleteSuggestion } from '@core/services/search.service';
import { forkJoin } from 'rxjs';

export interface SubCategory {
  name: string;
  slug: string;
  brandId?: string;
  priceRangeId?: string;
  filterType?: string;
  minPrice?: number;
  maxPrice?: number;
}

export interface MegaMenuSection {
  title: string;
  showTitle?: boolean;
  items: SubCategory[];
}

export interface Category {
  id: string;
  name: string;
  slug: string;
  icon?: string;
  departmentSlug: string;
  megaMenu?: MegaMenuSection[];
}

@Component({
  selector: 'app-category-nav',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './category-nav.component.html',
  styleUrls: ['./category-nav.component.scss']
})
export class CategoryNavComponent implements OnInit, OnDestroy {
  private categoryService = inject(CategoryService);
  private specialCollectionsService = inject(SpecialCollectionsService);
  private searchService = inject(SearchService);
  private router = inject(Router);
  private elementRef = inject(ElementRef);

  // Cleanup subject for subscriptions
  private destroy$ = new Subject<void>();

  // Category navigation state
  searchQuery = signal('');
  activeCategory = signal<string | null>(null);
  hoveredCategory = signal<string | null>(null);
  isLoading = signal(false);
  error = signal<string | null>(null);

  // Autocomplete state
  autocompleteSuggestions = signal<AutocompleteSuggestion[]>([]);
  showAutocomplete = signal(false);
  isSearching = signal(false);
  selectedSuggestionIndex = signal(-1);

  categories: Category[] = [];
  categoryDict: Record<string, Category> = {};
  specialCollections = signal<SpecialCollectionDto[]>([]);

  ngOnInit(): void {
    // Only load categories if not on auth or tenant-not-found pages
    if (typeof window !== 'undefined') {
      const path = window.location.pathname;
      if (!path.includes('/auth/') && !path.includes('/tenant-not-found')) {
        this.loadCategories();
      }
    }

    // Subscribe to autocomplete stream
    this.searchService.getAutocompleteStream()
      .pipe(takeUntil(this.destroy$))
      .subscribe(suggestions => {
        this.autocompleteSuggestions.set(suggestions);
        this.showAutocomplete.set(suggestions.length > 0);
        this.isSearching.set(false);
        this.selectedSuggestionIndex.set(-1);
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * Handles click outside to close autocomplete dropdown
   */
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    const searchContainer = this.elementRef.nativeElement.querySelector('.search-container');
    if (searchContainer && !searchContainer.contains(target)) {
      this.closeAutocomplete();
    }
  }

  private loadCategories(): void {
    this.isLoading.set(true);
    this.error.set(null);

    forkJoin({
      megaMenu: this.categoryService.getMegaMenuData(),
      specialCollections: this.specialCollectionsService.getSpecialCollections('cricket')
    }).subscribe({
      next: ({ megaMenu, specialCollections }) => {
        this.specialCollections.set(specialCollections.collections);

        const regularCategories = megaMenu.departments.flatMap(dept =>
          dept.categories.map(item => {
            const sections = item.sections.map(section => ({
              title: section.title,
              showTitle: section.showTitle,
              items: section.items.map(subCategory => ({
                name: subCategory.name,
                slug: subCategory.slug,
                minPrice: subCategory.minPrice,
                maxPrice: subCategory.maxPrice,
                brandId: subCategory.brandId,
                priceRangeId: subCategory.priceRangeId,
                filterType: subCategory.filterType
              }))
            }));

            return {
              id: item.category.id,
              name: item.category.name,
              slug: item.category.slug,
              icon: item.category.icon,
              departmentSlug: dept.slug,
              megaMenu: sections
            };
          })
        );

        const dealsCategory: Category[] = specialCollections.collections.length > 0 && megaMenu.departments.length > 0
          ? [{
              id: 'deals',
              name: 'Deals',
              slug: 'deals',
              icon: '',
              departmentSlug: megaMenu.departments[0].slug,
              megaMenu: [{
                title: 'Special Collections',
                showTitle: false,
                items: specialCollections.collections.map(collection => ({
                  name: `${collection.icon} ${collection.label}`,
                  slug: collection.slug,
                  filterType: 'SPECIAL_COLLECTION',
                  minPrice: undefined,
                  maxPrice: undefined,
                  brandId: undefined,
                  priceRangeId: undefined
                }))
              }]
            }]
          : [];

        this.categories = [...regularCategories, ...dealsCategory];

        this.categoryDict = this.categories.reduce((acc, category) => {
          acc[category.id] = category;
          return acc;
        }, {} as Record<string, Category>);

        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load menu data:', err);
        this.error.set('Failed to load menu');
        this.isLoading.set(false);
      }
    });
  }

  // ============================================================================
  // Search & Autocomplete Methods
  // ============================================================================

  /**
   * Handles search input changes - triggers autocomplete
   */
  onSearchInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const value = input.value;
    this.searchQuery.set(value);

    if (value.length >= 2) {
      this.isSearching.set(true);
      this.searchService.updateAutocompleteInput(value);
    } else {
      this.closeAutocomplete();
    }
  }

  /**
   * Handles keyboard navigation in autocomplete dropdown
   */
  onSearchKeydown(event: KeyboardEvent): void {
    const suggestions = this.autocompleteSuggestions();
    const currentIndex = this.selectedSuggestionIndex();

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        if (currentIndex < suggestions.length - 1) {
          this.selectedSuggestionIndex.set(currentIndex + 1);
        }
        break;

      case 'ArrowUp':
        event.preventDefault();
        if (currentIndex > 0) {
          this.selectedSuggestionIndex.set(currentIndex - 1);
        }
        break;

      case 'Enter':
        event.preventDefault();
        if (currentIndex >= 0 && currentIndex < suggestions.length) {
          this.selectSuggestion(suggestions[currentIndex]);
        } else {
          this.onSearch();
        }
        break;

      case 'Escape':
        this.closeAutocomplete();
        break;
    }
  }

  /**
   * Handles focusing on the search input
   */
  onSearchFocus(): void {
    const query = this.searchQuery();
    if (query.length >= 2 && this.autocompleteSuggestions().length > 0) {
      this.showAutocomplete.set(true);
    }
  }

  /**
   * Executes full product search and navigates to results
   */
  onSearch(): void {
    const query = this.searchQuery();
    this.closeAutocomplete();

    if (query.trim()) {
      this.router.navigate(['/search'], { queryParams: { q: query } });
    }
  }

  /**
   * Handles selection of an autocomplete suggestion
   */
  selectSuggestion(suggestion: AutocompleteSuggestion): void {
    this.closeAutocomplete();

    switch (suggestion.type) {
      case 'brand':
        // Navigate to search with brand filter
        this.router.navigate(['/search'], {
          queryParams: { brand: suggestion.slug || suggestion.text.toLowerCase().replace(/\s+/g, '-') }
        });
        break;

      case 'category':
        // Navigate to search with category filter
        this.router.navigate(['/search'], {
          queryParams: { category: suggestion.slug || suggestion.text.toLowerCase().replace(/\s+/g, '-') }
        });
        break;

      case 'product':
        // Search by product name to show it in results with similar items
        this.searchQuery.set(suggestion.text);
        this.router.navigate(['/search'], { queryParams: { q: suggestion.text } });
        break;

      default:
        this.searchQuery.set(suggestion.text);
        this.onSearch();
    }
  }

  /**
   * Closes autocomplete dropdown and clears state
   */
  closeAutocomplete(): void {
    this.showAutocomplete.set(false);
    this.autocompleteSuggestions.set([]);
    this.selectedSuggestionIndex.set(-1);
    this.isSearching.set(false);
    this.searchService.clearAutocomplete();
  }

  /**
   * Gets the icon for suggestion type
   */
  getSuggestionIcon(type: string): string {
    switch (type) {
      case 'brand': return 'tag';
      case 'category': return 'folder';
      case 'product': return 'box';
      default: return 'search';
    }
  }

  // ============================================================================
  // Category Navigation Methods
  // ============================================================================

  onCategoryHover(categoryId: string): void {
    this.hoveredCategory.set(categoryId);
  }

  onCategoryLeave(): void {
    this.hoveredCategory.set(null);
  }

  onCategoryClick(category: Category): void {
    console.log('Category clicked:', category);
  }

  onSubCategoryClick(category: Category, subCategory: SubCategory): void {
    this.hoveredCategory.set(null);

    let routePath: string[];
    const queryParams: any = {};

    switch (subCategory.filterType) {
      case 'BRAND':
        routePath = ['/', category.departmentSlug, category.slug, 'brand', subCategory.slug];
        break;

      case 'PRICE_RANGE':
        const priceRange = `${subCategory.minPrice}-${subCategory.maxPrice}`;
        routePath = ['/', category.departmentSlug, category.slug, 'price', priceRange];
        break;

      case 'SPECIAL_COLLECTION':
        routePath = ['/', category.departmentSlug, subCategory.slug];
        break;

      default:
        routePath = ['/', category.departmentSlug, category.slug, subCategory.slug];
    }

    this.router.navigate(routePath, { queryParams });
  }

  getActiveCategory(): Category | undefined {
    const categoryId = this.hoveredCategory();
    if (!categoryId) return undefined;
    return this.categoryDict[categoryId];
  }
}
