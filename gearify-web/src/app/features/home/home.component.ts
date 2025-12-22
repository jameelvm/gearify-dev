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
        sku: 'BAT-001',
        name: 'Professional English Willow Cricket Bat',
        description: 'Premium grade English willow bat with excellent stroke play',
        category: 'Cricket Bats',
        brand: 'SS',
        price: 299.99,
        compareAtPrice: 399.99,
        currency: 'USD',
        imageUrls: ['https://images.unsplash.com/photo-1624526267942-ab0ff8a3e972?w=400&h=400&fit=crop'],
        tags: ['bat', 'cricket', 'featured', 'english-willow'],
        attributes: {
          'Weight': '1150-1200g',
          'Wood Type': 'English Willow',
          'Handle': 'Cane Handle'
        },
        isActive: true,
        stockQuantity: 15,
        rating: {
          average: 4.8,
          count: 127
        },
        createdAt: new Date(),
        updatedAt: new Date()
      },
      {
        id: '2',
        tenantId: 'demo',
        sku: 'BALL-001',
        name: 'Premium Leather Cricket Ball',
        description: 'Hand-stitched genuine leather cricket ball for professional play',
        category: 'Cricket Balls',
        brand: 'Kookaburra',
        price: 29.99,
        compareAtPrice: 39.99,
        currency: 'USD',
        imageUrls: ['https://images.unsplash.com/photo-1531415074968-036ba1b575da?w=400&h=400&fit=crop'],
        tags: ['ball', 'cricket', 'featured', 'leather'],
        attributes: {
          'Weight': '156g',
          'Material': 'Genuine Leather',
          'Type': 'Match Quality'
        },
        isActive: true,
        stockQuantity: 50,
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
        sku: 'PAD-001',
        name: 'Professional Batting Pads',
        description: 'Lightweight batting pads with superior protection',
        category: 'Cricket Protection',
        brand: 'Gray-Nicolls',
        price: 149.99,
        compareAtPrice: 199.99,
        currency: 'USD',
        imageUrls: ['https://images.unsplash.com/photo-1593341646782-e0b495cff86d?w=400&h=400&fit=crop'],
        tags: ['pads', 'cricket', 'featured', 'protection'],
        attributes: {
          'Size': 'Adult',
          'Weight': 'Lightweight',
          'Material': 'High-density foam'
        },
        isActive: true,
        stockQuantity: 25,
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
        sku: 'GLOVE-001',
        name: 'Premium Batting Gloves',
        description: 'Professional batting gloves with superior grip and comfort',
        category: 'Cricket Gloves',
        brand: 'SG',
        price: 79.99,
        compareAtPrice: 99.99,
        currency: 'USD',
        imageUrls: ['https://images.unsplash.com/photo-1593341646591-4cbb2619ae50?w=400&h=400&fit=crop'],
        tags: ['gloves', 'cricket', 'featured', 'batting'],
        attributes: {
          'Size': 'Adult',
          'Material': 'Premium Leather',
          'Palm': 'Dual-layer protection'
        },
        isActive: true,
        stockQuantity: 30,
        rating: {
          average: 4.5,
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
