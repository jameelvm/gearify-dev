import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Product } from '@core/models/product.model';
import { ProductService } from '@core/services/product.service';
import { CartService } from '@core/services/cart.service';
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
import { SimilarProductsComponent } from './components/similar-products/similar-products.component';
import { ComplementaryProductsComponent } from './components/complementary-products/complementary-products.component';

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
    BreadcrumbComponent,
    SimilarProductsComponent,
    ComplementaryProductsComponent
  ],
  templateUrl: './product-detail.component.html',
  styleUrl: './product-detail.component.scss'
})
export class ProductDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private productService = inject(ProductService);
  private cartService = inject(CartService);

  // Loading and error states
  isLoading = signal<boolean>(true);
  error = signal<string | null>(null);

  // Active tab
  activeTab = signal<TabType>('description');

  // Quantity selector
  quantity = signal<number>(1);

  // Wishlist state
  isWishlisted = signal<boolean>(false);

  // Add to cart loading state
  isAddingToCart = signal<boolean>(false);

  // Product data from API
  product = signal<Product | null>(null);

  // Reviews (placeholder for future API integration)
  reviews = signal<Review[]>([]);

  // Current product ID for recommendation components
  currentProductId = signal<string>('');

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      const productId = params['id'];
      if (productId) {
        this.currentProductId.set(productId);
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
      },
      error: (err) => {
        console.error('Error loading product:', err);
        this.error.set('Failed to load product. Please try again.');
        this.isLoading.set(false);
      }
    });
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
    if (this.isOutOfStock() || this.isAddingToCart()) return;

    const prod = this.product();
    if (!prod) return;

    this.isAddingToCart.set(true);

    this.cartService.addToCartWithProduct({
      id: prod.id,
      name: prod.name,
      sku: prod.sku,
      price: prod.price,
      imageUrl: prod.thumbnailUrl || prod.imageUrls?.[0] || '',
      brand: prod.brand
    }, this.quantity()).subscribe({
      next: () => {
        this.isAddingToCart.set(false);
      },
      error: (err) => {
        console.error('Failed to add to cart:', err);
        this.isAddingToCart.set(false);
      }
    });
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
    this.cartService.addToCartWithProduct({
      id: product.id,
      name: product.name,
      sku: product.sku,
      price: product.price,
      imageUrl: product.thumbnailUrl || product.imageUrls?.[0] || '',
      brand: product.brand
    }, 1).subscribe({
      next: () => {
        console.log('Related product added to cart successfully');
      },
      error: (err) => {
        console.error('Failed to add related product to cart:', err);
      }
    });
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
