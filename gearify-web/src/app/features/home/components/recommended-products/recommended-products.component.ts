import { Component, OnInit, inject, signal, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { RecommendationService, ProductRecommendation } from '@core/services/recommendation.service';
import { ProductCardComponent } from '@app/ui-kit/components';
import { Product } from '@core/models/product.model';

@Component({
  selector: 'app-recommended-products',
  standalone: true,
  imports: [CommonModule, RouterLink, ProductCardComponent],
  templateUrl: './recommended-products.component.html',
  styleUrl: './recommended-products.component.scss'
})
export class RecommendedProductsComponent implements OnInit {
  @Input() title = 'Recommended for You';
  @Input() limit = 8;
  @Input() showViewAll = true;
  @Output() productClicked = new EventEmitter<Product>();
  @Output() addToCart = new EventEmitter<Product>();
  @Output() toggleWishlist = new EventEmitter<Product>();

  private recommendationService = inject(RecommendationService);

  products = signal<Product[]>([]);
  source = signal<string>('');
  loading = signal(true);
  error = signal(false);

  ngOnInit(): void {
    this.loadRecommendations();
  }

  private loadRecommendations(): void {
    this.loading.set(true);
    this.error.set(false);

    this.recommendationService.getForYou(this.limit).subscribe({
      next: (response) => {
        this.products.set(response.items.map(this.toProduct));
        this.source.set(response.source);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load recommendations:', err);
        this.error.set(true);
        this.loading.set(false);
      }
    });
  }

  onProductClick(product: Product): void {
    this.productClicked.emit(product);
  }

  onAddToCart(product: Product): void {
    this.addToCart.emit(product);
  }

  onToggleWishlist(product: Product): void {
    this.toggleWishlist.emit(product);
  }

  retry(): void {
    this.loadRecommendations();
  }

  private toProduct(item: ProductRecommendation): Product {
    return {
      id: item.productId,
      tenantId: '',
      sku: '',
      name: item.name,
      description: '',
      category: item.category,
      brand: item.brand,
      price: item.price,
      compareAtPrice: item.price,
      currency: 'INR',
      thumbnailUrl: item.thumbnailUrl ?? undefined,
      imageUrls: item.thumbnailUrl ? [item.thumbnailUrl] : [],
      tags: [],
      attributes: {},
      isActive: true,
      createdAt: new Date(),
      updatedAt: new Date()
    };
  }
}
