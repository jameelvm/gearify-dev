import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Product } from '@core/models/product.model';
import { PriceTagComponent } from '../price-tag/price-tag.component';
import { RatingComponent } from '../rating/rating.component';
import { BadgeComponent } from '../badge/badge.component';

/**
 * Product card component for displaying product information in grid/list views
 *
 * @example
 * <app-product-card
 *   [product]="product"
 *   (productClicked)="onProductClick($event)"
 *   (addToCart)="onAddToCart($event)"
 *   (toggleWishlist)="onToggleWishlist($event)">
 * </app-product-card>
 */
@Component({
  selector: 'app-product-card',
  standalone: true,
  imports: [
    CommonModule,
    PriceTagComponent,
    RatingComponent,
    BadgeComponent
  ],
  templateUrl: './product-card.component.html',
  styleUrl: './product-card.component.scss'
})
export class ProductCardComponent {
  @Input({ required: true }) product!: Product;
  @Input() compact = false;
  @Input() isWishlisted = false;

  @Output() productClicked = new EventEmitter<Product>();
  @Output() addToCart = new EventEmitter<Product>();
  @Output() toggleWishlist = new EventEmitter<Product>();

  imageLoaded = false;
  imageErrorOccurred = false;

  /**
   * Placeholder image as data URI to avoid missing file requests
   */
  private readonly placeholderImage = 'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNDAwIiBoZWlnaHQ9IjQwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48cmVjdCB3aWR0aD0iNDAwIiBoZWlnaHQ9IjQwMCIgZmlsbD0iI2YwZjBmMCIvPjx0ZXh0IHg9IjUwJSIgeT0iNTAlIiBmb250LWZhbWlseT0iQXJpYWwiIGZvbnQtc2l6ZT0iMTgiIGZpbGw9IiM5OTkiIHRleHQtYW5jaG9yPSJtaWRkbGUiIGR5PSIuM2VtIj5ObyBJbWFnZTwvdGV4dD48L3N2Zz4=';

  /**
   * Get the primary image URL for the product
   * Prefers thumbnailUrl (updated by Media Service), falls back to imageUrls[0] for backward compatibility
   */
  get primaryImageUrl(): string {
    return this.product.thumbnailUrl || this.product.imageUrls?.[0] || this.placeholderImage;
  }

  /**
   * Check if product has a discount
   */
  get hasDiscount(): boolean {
    return this.product.compareAtPrice > this.product.price;
  }

  /**
   * Check if product is out of stock
   */
  get isOutOfStock(): boolean {
    return this.product.stockQuantity !== undefined && this.product.stockQuantity <= 0;
  }

  /**
   * Check if product has low stock (less than 10 items)
   */
  get hasLowStock(): boolean {
    return this.product.stockQuantity !== undefined &&
           this.product.stockQuantity > 0 &&
           this.product.stockQuantity < 10;
  }

  /**
   * Handle product card click
   */
  onCardClick(): void {
    this.productClicked.emit(this.product);
  }

  /**
   * Handle add to cart button click
   */
  onAddToCartClick(event: Event): void {
    event.stopPropagation();
    if (!this.isOutOfStock) {
      this.addToCart.emit(this.product);
    }
  }

  /**
   * Handle wishlist toggle button click
   */
  onWishlistClick(event: Event): void {
    event.stopPropagation();
    this.toggleWishlist.emit(this.product);
  }

  /**
   * Handle image load event
   */
  onImageLoad(): void {
    this.imageLoaded = true;
  }

  /**
   * Handle image error event
   */
  onImageError(event: Event): void {
    // Prevent infinite loop - only set placeholder once
    if (this.imageErrorOccurred) {
      return;
    }

    this.imageErrorOccurred = true;
    const img = event.target as HTMLImageElement;
    img.src = this.placeholderImage;
    this.imageLoaded = true;
  }
}
