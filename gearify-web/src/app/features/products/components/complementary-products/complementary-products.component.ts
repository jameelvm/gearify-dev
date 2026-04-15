import { Component, OnChanges, inject, signal, Input, Output, EventEmitter, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RecommendationService, ProductRecommendation } from '@core/services/recommendation.service';
import { ProductCardComponent } from '@app/ui-kit/components';
import { Product } from '@core/models/product.model';

@Component({
  selector: 'app-complementary-products',
  standalone: true,
  imports: [CommonModule, ProductCardComponent],
  templateUrl: './complementary-products.component.html',
  styleUrl: './complementary-products.component.scss'
})
export class ComplementaryProductsComponent implements OnChanges {
  @Input({ required: true }) productId!: string;
  @Input() limit = 4;
  @Output() productClicked = new EventEmitter<Product>();
  @Output() addToCart = new EventEmitter<Product>();

  private recommendationService = inject(RecommendationService);

  products = signal<Product[]>([]);
  loading = signal(true);

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['productId'] && this.productId) {
      this.loadComplementary();
    }
  }

  private loadComplementary(): void {
    this.loading.set(true);

    this.recommendationService.getComplementary(this.productId, this.limit).subscribe({
      next: (response) => {
        this.products.set(response.items.map(this.toProduct));
        this.loading.set(false);
      },
      error: () => {
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
