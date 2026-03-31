import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import {
  ButtonComponent,
  BrandBarComponent
} from '@app/ui-kit/components';
import { Product } from '@core/models/product.model';
import { RecommendedProductsComponent } from './components/recommended-products/recommended-products.component';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    ButtonComponent,
    BrandBarComponent,
    RecommendedProductsComponent
  ],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent implements OnInit {
  brands = signal<any[]>([]);
  categories = signal<any[]>([]);

  ngOnInit(): void {
    this.loadBrands();
    this.loadCategories();
  }

  private loadBrands(): void {
    this.brands.set([
      { id: '1', name: 'SS', logoUrl: 'https://via.placeholder.com/120x60?text=SS' },
      { id: '2', name: 'Kookaburra', logoUrl: 'https://via.placeholder.com/120x60?text=Kookaburra' },
      { id: '3', name: 'Gray-Nicolls', logoUrl: 'https://via.placeholder.com/120x60?text=Gray-Nicolls' },
      { id: '4', name: 'SG', logoUrl: 'https://via.placeholder.com/120x60?text=SG' },
      { id: '5', name: 'MRF', logoUrl: 'https://via.placeholder.com/120x60?text=MRF' }
    ]);
  }

  private loadCategories(): void {
    this.categories.set([
      { id: '1', name: 'Cricket Bats', icon: '🏏', count: 150 },
      { id: '2', name: 'Cricket Balls', icon: '🔴', count: 230 },
      { id: '3', name: 'Protection Gear', icon: '🛡️', count: 180 },
      { id: '4', name: 'Cricket Clothing', icon: '👕', count: 95 },
      { id: '5', name: 'Training Equipment', icon: '🎯', count: 120 },
      { id: '6', name: 'Accessories', icon: '🎒', count: 85 }
    ]);
  }

  handleProductClick(product: Product): void {
    console.log('Product clicked:', product);
  }

  handleAddToCart(product: Product): void {
    console.log('Add to cart:', product);
  }

  handleToggleWishlist(product: Product): void {
    console.log('Toggle wishlist:', product);
  }

  handleBrandClick(brand: any): void {
    console.log('Brand clicked:', brand);
  }

  handleCategoryClick(category: any): void {
    console.log('Category clicked:', category);
  }
}
