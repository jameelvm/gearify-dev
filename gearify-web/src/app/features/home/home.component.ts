import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import {
  ButtonComponent,
  ProductCardComponent,
  BrandBarComponent,
  BadgeComponent
} from '@app/ui-kit/components';
import { Product } from '@core/models/product.model';

/**
 * Home Page Component
 * Main landing page with hero section, featured products, and categories
 */
@Component({
  selector: 'app-home',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    ButtonComponent,
    ProductCardComponent,
    BrandBarComponent,
    BadgeComponent
  ],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent implements OnInit {
  featuredProducts = signal<Product[]>([]);
  newArrivals = signal<Product[]>([]);
  brands = signal<any[]>([]);
  categories = signal<any[]>([]);

  ngOnInit(): void {
    this.loadFeaturedProducts();
    this.loadNewArrivals();
    this.loadBrands();
    this.loadCategories();
  }

  private loadFeaturedProducts(): void {
    // Mock data - replace with actual API call
    this.featuredProducts.set([
      {
        id: '1',
        tenantId: 'demo',
        sku: 'LAPTOP-001',
        name: 'Premium Laptop Pro 15"',
        description: 'High-performance laptop with stunning display',
        category: 'Electronics',
        brand: 'TechBrand',
        price: 1299.99,
        compareAtPrice: 1599.99,
        currency: 'USD',
        imageUrls: ['https://via.placeholder.com/400x400?text=Laptop'],
        tags: ['laptop', 'electronics', 'featured'],
        attributes: {
          'Screen Size': '15 inches',
          'RAM': '16GB',
          'Storage': '512GB SSD'
        },
        isActive: true,
        stockQuantity: 15,
        rating: {
          average: 4.5,
          count: 127
        },
        createdAt: new Date(),
        updatedAt: new Date()
      },
      {
        id: '2',
        tenantId: 'demo',
        sku: 'PHONE-001',
        name: 'Smartphone X Pro',
        description: 'Latest flagship smartphone with amazing camera',
        category: 'Electronics',
        brand: 'PhoneBrand',
        price: 899.99,
        compareAtPrice: 999.99,
        currency: 'USD',
        imageUrls: ['https://via.placeholder.com/400x400?text=Smartphone'],
        tags: ['phone', 'electronics', 'featured'],
        attributes: {
          'Screen Size': '6.5 inches',
          'RAM': '8GB',
          'Storage': '256GB'
        },
        isActive: true,
        stockQuantity: 25,
        rating: {
          average: 4.7,
          count: 89
        },
        createdAt: new Date(),
        updatedAt: new Date()
      },
      {
        id: '3',
        tenantId: 'demo',
        sku: 'HEADPHONE-001',
        name: 'Wireless Headphones Pro',
        description: 'Premium noise-cancelling wireless headphones',
        category: 'Audio',
        brand: 'AudioBrand',
        price: 349.99,
        compareAtPrice: 449.99,
        currency: 'USD',
        imageUrls: ['https://via.placeholder.com/400x400?text=Headphones'],
        tags: ['audio', 'headphones', 'featured'],
        attributes: {
          'Battery Life': '30 hours',
          'Noise Cancelling': 'Active',
          'Wireless': 'Bluetooth 5.0'
        },
        isActive: true,
        stockQuantity: 40,
        rating: {
          average: 4.6,
          count: 156
        },
        createdAt: new Date(),
        updatedAt: new Date()
      },
      {
        id: '4',
        tenantId: 'demo',
        sku: 'WATCH-001',
        name: 'Smart Watch Ultra',
        description: 'Advanced smartwatch with health tracking',
        category: 'Wearables',
        brand: 'WatchBrand',
        price: 599.99,
        compareAtPrice: 699.99,
        currency: 'USD',
        imageUrls: ['https://via.placeholder.com/400x400?text=Smartwatch'],
        tags: ['watch', 'wearables', 'featured'],
        attributes: {
          'Display': 'AMOLED',
          'Battery': '48 hours',
          'Water Resistant': '50m'
        },
        isActive: true,
        stockQuantity: 30,
        rating: {
          average: 4.4,
          count: 92
        },
        createdAt: new Date(),
        updatedAt: new Date()
      }
    ]);
  }

  private loadNewArrivals(): void {
    // Mock data - replace with actual API call
    this.newArrivals.set(this.featuredProducts().slice(0, 3));
  }

  private loadBrands(): void {
    this.brands.set([
      { id: '1', name: 'TechBrand', logoUrl: 'https://via.placeholder.com/120x60?text=TechBrand' },
      { id: '2', name: 'PhoneBrand', logoUrl: 'https://via.placeholder.com/120x60?text=PhoneBrand' },
      { id: '3', name: 'AudioBrand', logoUrl: 'https://via.placeholder.com/120x60?text=AudioBrand' },
      { id: '4', name: 'WatchBrand', logoUrl: 'https://via.placeholder.com/120x60?text=WatchBrand' },
      { id: '5', name: 'GameBrand', logoUrl: 'https://via.placeholder.com/120x60?text=GameBrand' }
    ]);
  }

  private loadCategories(): void {
    this.categories.set([
      { id: '1', name: 'Electronics', icon: '💻', count: 150 },
      { id: '2', name: 'Fashion', icon: '👕', count: 230 },
      { id: '3', name: 'Home & Garden', icon: '🏠', count: 180 },
      { id: '4', name: 'Sports', icon: '⚽', count: 95 },
      { id: '5', name: 'Books', icon: '📚', count: 340 },
      { id: '6', name: 'Toys', icon: '🎮', count: 120 }
    ]);
  }

  handleProductClick(product: Product): void {
    console.log('Product clicked:', product);
    // Navigate to product detail page
  }

  handleAddToCart(product: Product): void {
    console.log('Add to cart:', product);
    // Add to cart logic
  }

  handleToggleWishlist(product: Product): void {
    console.log('Toggle wishlist:', product);
    // Toggle wishlist logic
  }

  handleBrandClick(brand: any): void {
    console.log('Brand clicked:', brand);
    // Navigate to brand page
  }

  handleCategoryClick(category: any): void {
    console.log('Category clicked:', category);
    // Navigate to category page
  }
}
