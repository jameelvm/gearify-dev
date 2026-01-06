import { Component, signal, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CategoryService } from '@core/services/category.service';
import { SpecialCollectionsService, SpecialCollectionDto } from '@core/services/special-collections.service';
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
  departmentSlug: string; // Added to track department for URL construction
  megaMenu?: MegaMenuSection[];
}

@Component({
  selector: 'app-category-nav',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './category-nav.component.html',
  styleUrls: ['./category-nav.component.scss']
})
export class CategoryNavComponent implements OnInit {
  private categoryService = inject(CategoryService);
  private specialCollectionsService = inject(SpecialCollectionsService);
  private router = inject(Router);

  searchQuery = signal('');
  activeCategory = signal<string | null>(null);
  hoveredCategory = signal<string | null>(null);
  isLoading = signal(false);
  error = signal<string | null>(null);

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
  }

  private loadCategories(): void {
    this.isLoading.set(true);
    this.error.set(null);

    // Fetch both mega menu data and special collections in parallel
    // TODO: For multi-department, get departmentSlug from selected department
    forkJoin({
      megaMenu: this.categoryService.getMegaMenuData(),
      specialCollections: this.specialCollectionsService.getSpecialCollections('cricket')
    }).subscribe({
      next: ({ megaMenu, specialCollections }) => {
        // Store special collections
        this.specialCollections.set(specialCollections.collections);

        // Extract categories from departments and preserve department slug for routing
        // For single-department tenants, get categories from the first department
        // For multi-department, this would need UI to select department
        const regularCategories = megaMenu.departments.flatMap(dept =>
          dept.categories.map(item => {
            // Build category sections from mega menu data
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

        // Create independent "Deals" menu item with special collections
        const dealsCategory: Category[] = specialCollections.collections.length > 0 && megaMenu.departments.length > 0
          ? [{
              id: 'deals',
              name: 'Deals',
              slug: 'deals',
              icon: '🔥',
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
        // Fallback to empty array or show error message
      }
    });
  }

  onSearch(): void {
    const query = this.searchQuery();
    if (query.trim()) {
      console.log('Searching for:', query);
      // TODO: Navigate to search results or filter products
    }
  }

  onCategoryHover(categoryId: string): void {
    this.hoveredCategory.set(categoryId);
  }

  onCategoryLeave(): void {
    this.hoveredCategory.set(null);
  }

  onCategoryClick(category: Category): void {
    console.log('Category clicked:', category);
    // TODO: Navigate to category page or filter products
  }

  onSubCategoryClick(category: Category, subCategory: SubCategory): void {
    this.hoveredCategory.set(null);

    let routePath: string[];
    const queryParams: any = {};

    // Build route path based on FilterType
    switch (subCategory.filterType) {
      case 'BRAND':
        // For brand filters, use clean URL with brand slug
        routePath = ['/', category.departmentSlug, category.slug, 'brand', subCategory.slug];
        break;

      case 'PRICE_RANGE':
        // For price range filters, use category route with price range in URL
        const priceRange = `${subCategory.minPrice}-${subCategory.maxPrice}`;
        routePath = ['/', category.departmentSlug, category.slug, 'price', priceRange];
        break;

      case 'SPECIAL_COLLECTION':
        // For special collections, use department/collection route
        routePath = ['/', category.departmentSlug, subCategory.slug];
        break;

      default:
        // For regular subcategories, use full route path
        routePath = ['/', category.departmentSlug, category.slug, subCategory.slug];
    }

    this.router.navigate(routePath, { queryParams });
  }

  getActiveCategory(): Category | undefined {
    const categoryId = this.hoveredCategory();
    if (!categoryId) return undefined;
    var category = this.categoryDict[categoryId];
    return category;
  }
}
