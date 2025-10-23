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

  /**
   * Get the primary image URL for the product
   */
  get primaryImageUrl(): string {
    return this.product.imageUrls?.[0] || '/assets/images/placeholder-product.png';
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
    const img = event.target as HTMLImageElement;
    img.src = '/assets/images/placeholder-product.png';
    this.imageLoaded = true;
  }
}
