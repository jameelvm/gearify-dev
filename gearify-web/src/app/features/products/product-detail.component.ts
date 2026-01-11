import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Product } from '@core/models/product.model';
import { ProductService } from '@core/services/product.service';
import { SearchService, ProductSearchItem } from '@core/services/search.service';
import {
  ImageGalleryComponent,
  RatingComponent,
  ButtonComponent,
  ProductCardComponent,
  PriceTagComponent,
  BadgeComponent,
  BreadcrumbComponent
} from '@app/ui-kit/components';
import { GalleryImage } from '@app/ui-kit/components/image-gallery/image-gallery.component';

export type TabType = 'description' | 'reviews' | 'specifications';

export interface Review {
  id: string;
  userId: string;
  userName: string;
  userAvatar?: string;
  rating: number;
  title: string;
  comment: string;
  createdAt: Date;
  verified: boolean;
  helpful: number;
}

/**
 * Product detail page component
 */
@Component({
  selector: 'app-product-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    ImageGalleryComponent,
    RatingComponent,
    ButtonComponent,
    ProductCardComponent,
    PriceTagComponent,
    BadgeComponent,
    BreadcrumbComponent
  ],
  templateUrl: './product-detail.component.html',
  styleUrl: './product-detail.component.scss'
})
export class ProductDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private productService = inject(ProductService);
  private searchService = inject(SearchService);

  // Loading and error states
  isLoading = signal<boolean>(true);
  error = signal<string | null>(null);

  // Active tab
  activeTab = signal<TabType>('description');

  // Quantity selector
  quantity = signal<number>(1);

  // Wishlist state
  isWishlisted = signal<boolean>(false);

  // Product data from API
  product = signal<Product | null>(null);

  // Reviews (placeholder for future API integration)
  reviews = signal<Review[]>([]);

  // Related products (placeholder for future API integration)
  relatedProducts = signal<Product[]>([]);

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      const productId = params['id'];
      if (productId) {
        this.loadProduct(productId);
      }
    });
  }

  loadProduct(id: string): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.productService.getProductById(id).subscribe({
      next: (product) => {
        this.product.set(product);
        this.isLoading.set(false);
        // Load similar products based on category
        this.loadSimilarProducts(product);
      },
      error: (err) => {
        console.error('Error loading product:', err);
        this.error.set('Failed to load product. Please try again.');
        this.isLoading.set(false);
      }
    });
  }

  /**
   * Load similar products using dedicated API endpoint
   * Backend uses brand, category, price range, and tags for matching
   */
  private loadSimilarProducts(currentProduct: Product): void {
    this.searchService.getSimilarProducts(currentProduct.id, 4).subscribe({
      next: (response) => {
        const similar = response.items.map(item => this.mapSearchItemToProduct(item));
        this.relatedProducts.set(similar);
      },
      error: (err) => {
        console.error('Error loading similar products:', err);
      }
    });
  }

  /**
   * Map ProductSearchItem to Product for ProductCardComponent
   */
  private mapSearchItemToProduct(item: ProductSearchItem): Product {
    return {
      id: item.id,
      tenantId: '',
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
    };
  }

  // Computed gallery images - use enriched images if available, fallback to imageUrls or thumbnailUrl
  galleryImages = computed<GalleryImage[]>(() => {
    const prod = this.product();
    if (!prod) return [];

    // Use enriched images from Media Service if available
    if (prod.images && prod.images.length > 0) {
      return prod.images.map((img, index) => ({
        url: img.largeUrl || img.mediumUrl || img.originalUrl,
        alt: img.altText || `${prod.name} - Image ${index + 1}`,
        thumbnail: img.thumbnailUrl || img.mediumUrl
      }));
    }

    // Fallback to legacy imageUrls if available
    if (prod.imageUrls && prod.imageUrls.length > 0) {
      return prod.imageUrls.map((url, index) => ({
        url,
        alt: `${prod.name} - Image ${index + 1}`,
        thumbnail: url
      }));
    }

    // Last resort: use thumbnailUrl if available
    if (prod.thumbnailUrl) {
      return [{
        url: prod.thumbnailUrl,
        alt: prod.name,
        thumbnail: prod.thumbnailUrl
      }];
    }

    return [];
  });

  // Computed properties
  hasDiscount = computed(() => {
    const prod = this.product();
    if (!prod) return false;
    return prod.compareAtPrice > prod.price;
  });

  discountPercent = computed(() => {
    const prod = this.product();
    if (!prod || !this.hasDiscount()) return 0;
    return Math.round(((prod.compareAtPrice - prod.price) / prod.compareAtPrice) * 100);
  });

  isOutOfStock = computed(() => {
    const prod = this.product();
    if (!prod) return false;
    const stock = prod.stockQuantity;
    return stock !== undefined && stock <= 0;
  });

  hasLowStock = computed(() => {
    const prod = this.product();
    if (!prod) return false;
    const stock = prod.stockQuantity;
    return stock !== undefined && stock > 0 && stock < 10;
  });

  // Average rating from product
  averageRating = computed(() => {
    const prod = this.product();
    if (!prod) return 0;
    return prod.ratingAverage ?? prod.rating?.average ?? 0;
  });

  // Rating count from product
  ratingCount = computed(() => {
    const prod = this.product();
    if (!prod) return 0;
    return prod.ratingCount ?? prod.rating?.count ?? 0;
  });

  // Rating distribution (placeholder - would come from reviews API)
  ratingDistribution = computed(() => {
    const distribution = { 5: 0, 4: 0, 3: 0, 2: 0, 1: 0 };
    const reviewList = this.reviews();

    reviewList.forEach(review => {
      distribution[review.rating as keyof typeof distribution]++;
    });

    return Object.entries(distribution)
      .reverse()
      .map(([rating, count]) => ({
        rating: parseInt(rating),
        count,
        percentage: reviewList.length > 0 ? (count / reviewList.length) * 100 : 0
      }));
  });

  /**
   * Set active tab
   */
  setActiveTab(tab: TabType): void {
    this.activeTab.set(tab);
  }

  /**
   * Increase quantity
   */
  increaseQuantity(): void {
    const prod = this.product();
    if (!prod) return;
    const stock = prod.stockQuantity || 0;
    if (this.quantity() < stock) {
      this.quantity.update(q => q + 1);
    }
  }

  /**
   * Decrease quantity
   */
  decreaseQuantity(): void {
    if (this.quantity() > 1) {
      this.quantity.update(q => q - 1);
    }
  }

  /**
   * Handle add to cart
   */
  onAddToCart(): void {
    if (this.isOutOfStock()) return;

    console.log('Adding to cart:', {
      product: this.product(),
      quantity: this.quantity()
    });
    // Implement cart service call
  }

  /**
   * Handle buy now
   */
  onBuyNow(): void {
    if (this.isOutOfStock()) return;

    console.log('Buy now:', {
      product: this.product(),
      quantity: this.quantity()
    });
    // Implement checkout navigation
  }

  /**
   * Toggle wishlist
   */
  toggleWishlist(): void {
    this.isWishlisted.update(val => !val);
    console.log('Wishlist toggled:', this.isWishlisted());
    // Implement wishlist service call
  }

  /**
   * Handle product card click (related products)
   */
  onRelatedProductClick(product: Product): void {
    this.router.navigate(['/products', product.id]);
  }

  /**
   * Handle add to cart for related products
   */
  onRelatedProductAddToCart(product: Product): void {
    console.log('Add related product to cart:', product);
    // Implement cart service call
  }

  /**
   * Handle review helpful click
   */
  onReviewHelpful(review: Review): void {
    console.log('Review marked as helpful:', review);
    // Implement helpful vote
  }

  /**
   * Handle write review
   */
  onWriteReview(): void {
    console.log('Write review clicked');
    // Open review modal/form
  }
}
