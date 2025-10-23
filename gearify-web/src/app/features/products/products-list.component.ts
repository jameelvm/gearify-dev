import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Product, ProductFilter } from '@core/models/product.model';
import {
  ProductCardComponent,
  PaginationComponent,
  InputComponent,
  SelectComponent,
  ButtonComponent,
  CheckboxComponent
} from '@app/ui-kit/components';
import { PageChangeEvent } from '@app/ui-kit/components/pagination/pagination.component';
import { SelectOption } from '@app/ui-kit/components/select/select.component';

export type ViewMode = 'grid' | 'list';
export type SortField = 'price' | 'rating' | 'newest' | 'name';

interface PriceRange {
  min: number;
  max: number;
}

/**
 * Product listing page with filtering, search, sorting, and pagination
 */
@Component({
  selector: 'app-products-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ProductCardComponent,
    PaginationComponent,
    InputComponent,
    SelectComponent,
    ButtonComponent,
    CheckboxComponent
  ],
  templateUrl: './products-list.component.html',
  styleUrl: './products-list.component.scss'
})
export class ProductsListComponent {
  // View state
  viewMode = signal<ViewMode>('grid');
  searchQuery = signal<string>('');
  selectedCategory = signal<string>('');
  selectedBrand = signal<string>('');
  priceRange = signal<PriceRange>({ min: 0, max: 10000 });
  sortField = signal<SortField>('newest');

  // Pagination state
  currentPage = signal<number>(1);
  itemsPerPage = signal<number>(12);

  // Filter visibility (for mobile)
  showFilters = signal<boolean>(true);

  // Mock product data
  private allProducts = signal<Product[]>([
    {
      id: '1',
      tenantId: 'tenant1',
      sku: 'LAPTOP-001',
      name: 'UltraBook Pro 15',
      description: 'Powerful laptop with 15-inch display, Intel Core i7, 16GB RAM, and 512GB SSD. Perfect for professionals and content creators.',
      category: 'Electronics',
      brand: 'TechPro',
      price: 1299.99,
      compareAtPrice: 1499.99,
      currency: 'USD',
      imageUrls: [
        'https://images.unsplash.com/photo-1496181133206-80ce9b88a853?w=800',
        'https://images.unsplash.com/photo-1517336714731-489689fd1ca8?w=800'
      ],
      tags: ['laptop', 'computer', 'portable'],
      attributes: { processor: 'Intel Core i7', ram: '16GB', storage: '512GB SSD' },
      isActive: true,
      stockQuantity: 45,
      rating: { average: 4.5, count: 127 },
      createdAt: new Date('2024-01-15'),
      updatedAt: new Date('2024-01-15')
    },
    {
      id: '2',
      tenantId: 'tenant1',
      sku: 'PHONE-002',
      name: 'SmartPhone X Pro',
      description: 'Latest flagship smartphone with 6.7-inch OLED display, 5G connectivity, and professional-grade camera system.',
      category: 'Electronics',
      brand: 'MobileTech',
      price: 999.99,
      compareAtPrice: 1099.99,
      currency: 'USD',
      imageUrls: [
        'https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?w=800',
        'https://images.unsplash.com/photo-1598327105666-5b89351aff97?w=800'
      ],
      tags: ['smartphone', 'mobile', '5g'],
      attributes: { screen: '6.7-inch OLED', camera: '108MP', battery: '5000mAh' },
      isActive: true,
      stockQuantity: 78,
      rating: { average: 4.7, count: 234 },
      createdAt: new Date('2024-02-01'),
      updatedAt: new Date('2024-02-01')
    },
    {
      id: '3',
      tenantId: 'tenant1',
      sku: 'HEADSET-003',
      name: 'Wireless Noise-Canceling Headphones',
      description: 'Premium over-ear headphones with active noise cancellation, 30-hour battery life, and studio-quality sound.',
      category: 'Audio',
      brand: 'SoundWave',
      price: 349.99,
      compareAtPrice: 399.99,
      currency: 'USD',
      imageUrls: [
        'https://images.unsplash.com/photo-1505740420928-5e560c06d30e?w=800',
        'https://images.unsplash.com/photo-1484704849700-f032a568e944?w=800'
      ],
      tags: ['headphones', 'wireless', 'noise-canceling'],
      attributes: { battery: '30 hours', connectivity: 'Bluetooth 5.0', driver: '40mm' },
      isActive: true,
      stockQuantity: 120,
      rating: { average: 4.6, count: 89 },
      createdAt: new Date('2024-01-20'),
      updatedAt: new Date('2024-01-20')
    },
    {
      id: '4',
      tenantId: 'tenant1',
      sku: 'WATCH-004',
      name: 'Fitness Smartwatch Pro',
      description: 'Advanced fitness tracker with heart rate monitoring, GPS, sleep tracking, and 7-day battery life.',
      category: 'Wearables',
      brand: 'FitGear',
      price: 249.99,
      compareAtPrice: 299.99,
      currency: 'USD',
      imageUrls: [
        'https://images.unsplash.com/photo-1523275335684-37898b6baf30?w=800',
        'https://images.unsplash.com/photo-1546868871-7041f2a55e12?w=800'
      ],
      tags: ['smartwatch', 'fitness', 'health'],
      attributes: { display: '1.4-inch AMOLED', sensors: 'Heart Rate, GPS, SpO2', waterproof: 'IP68' },
      isActive: true,
      stockQuantity: 5,
      rating: { average: 4.3, count: 156 },
      createdAt: new Date('2024-01-25'),
      updatedAt: new Date('2024-01-25')
    },
    {
      id: '5',
      tenantId: 'tenant1',
      sku: 'TABLET-005',
      name: 'Pro Tablet 12.9',
      description: 'Professional-grade tablet with 12.9-inch Retina display, M2 chip, and support for stylus and keyboard.',
      category: 'Electronics',
      brand: 'TechPro',
      price: 1099.99,
      compareAtPrice: 1199.99,
      currency: 'USD',
      imageUrls: [
        'https://images.unsplash.com/photo-1561154464-82e9adf32764?w=800',
        'https://images.unsplash.com/photo-1544244015-0df4b3ffc6b0?w=800'
      ],
      tags: ['tablet', 'creative', 'portable'],
      attributes: { screen: '12.9-inch Retina', chip: 'M2', storage: '256GB' },
      isActive: true,
      stockQuantity: 0,
      rating: { average: 4.8, count: 92 },
      createdAt: new Date('2024-02-05'),
      updatedAt: new Date('2024-02-05')
    },
    {
      id: '6',
      tenantId: 'tenant1',
      sku: 'CAMERA-006',
      name: 'Mirrorless Camera Kit',
      description: 'Professional mirrorless camera with 24MP sensor, 4K video, and 18-55mm lens included.',
      category: 'Photography',
      brand: 'PhotoPro',
      price: 1499.99,
      compareAtPrice: 1699.99,
      currency: 'USD',
      imageUrls: [
        'https://images.unsplash.com/photo-1516035069371-29a1b244cc32?w=800',
        'https://images.unsplash.com/photo-1502920917128-1aa500764cbd?w=800'
      ],
      tags: ['camera', 'photography', 'professional'],
      attributes: { sensor: '24MP APS-C', video: '4K 60fps', lens: '18-55mm f/3.5-5.6' },
      isActive: true,
      stockQuantity: 32,
      rating: { average: 4.9, count: 67 },
      createdAt: new Date('2024-01-10'),
      updatedAt: new Date('2024-01-10')
    },
    {
      id: '7',
      tenantId: 'tenant1',
      sku: 'SPEAKER-007',
      name: 'Portable Bluetooth Speaker',
      description: 'Waterproof portable speaker with 360-degree sound, 12-hour battery, and built-in power bank.',
      category: 'Audio',
      brand: 'SoundWave',
      price: 129.99,
      compareAtPrice: 159.99,
      currency: 'USD',
      imageUrls: [
        'https://images.unsplash.com/photo-1608043152269-423dbba4e7e1?w=800',
        'https://images.unsplash.com/photo-1589003077984-894e133dabab?w=800'
      ],
      tags: ['speaker', 'bluetooth', 'portable', 'waterproof'],
      attributes: { battery: '12 hours', waterproof: 'IPX7', power: '20W' },
      isActive: true,
      stockQuantity: 89,
      rating: { average: 4.4, count: 203 },
      createdAt: new Date('2024-02-10'),
      updatedAt: new Date('2024-02-10')
    },
    {
      id: '8',
      tenantId: 'tenant1',
      sku: 'KEYBOARD-008',
      name: 'Mechanical Gaming Keyboard',
      description: 'RGB mechanical keyboard with tactile switches, programmable keys, and aluminum frame.',
      category: 'Gaming',
      brand: 'GameGear',
      price: 159.99,
      compareAtPrice: 189.99,
      currency: 'USD',
      imageUrls: [
        'https://images.unsplash.com/photo-1511467687858-23d96c32e4ae?w=800',
        'https://images.unsplash.com/photo-1587829741301-dc798b83add3?w=800'
      ],
      tags: ['keyboard', 'gaming', 'mechanical', 'rgb'],
      attributes: { switches: 'Cherry MX Brown', backlight: 'RGB', layout: 'Full-size' },
      isActive: true,
      stockQuantity: 64,
      rating: { average: 4.5, count: 178 },
      createdAt: new Date('2024-01-30'),
      updatedAt: new Date('2024-01-30')
    },
    {
      id: '9',
      tenantId: 'tenant1',
      sku: 'MOUSE-009',
      name: 'Wireless Gaming Mouse',
      description: 'High-precision wireless gaming mouse with 16000 DPI, 6 programmable buttons, and RGB lighting.',
      category: 'Gaming',
      brand: 'GameGear',
      price: 79.99,
      compareAtPrice: 99.99,
      currency: 'USD',
      imageUrls: [
        'https://images.unsplash.com/photo-1527814050087-3793815479db?w=800',
        'https://images.unsplash.com/photo-1615663245857-ac93bb7c39e7?w=800'
      ],
      tags: ['mouse', 'gaming', 'wireless'],
      attributes: { dpi: '16000', buttons: '6 programmable', battery: '70 hours' },
      isActive: true,
      stockQuantity: 156,
      rating: { average: 4.6, count: 234 },
      createdAt: new Date('2024-02-15'),
      updatedAt: new Date('2024-02-15')
    },
    {
      id: '10',
      tenantId: 'tenant1',
      sku: 'MONITOR-010',
      name: '4K Ultra HD Monitor 27"',
      description: '27-inch 4K IPS monitor with HDR, 99% sRGB color gamut, and USB-C connectivity.',
      category: 'Electronics',
      brand: 'TechPro',
      price: 449.99,
      compareAtPrice: 549.99,
      currency: 'USD',
      imageUrls: [
        'https://images.unsplash.com/photo-1527443224154-c4a3942d3acf?w=800',
        'https://images.unsplash.com/photo-1585792180666-f7347c490ee2?w=800'
      ],
      tags: ['monitor', 'display', '4k', 'professional'],
      attributes: { size: '27-inch', resolution: '3840x2160', panel: 'IPS', refresh: '60Hz' },
      isActive: true,
      stockQuantity: 28,
      rating: { average: 4.7, count: 145 },
      createdAt: new Date('2024-01-05'),
      updatedAt: new Date('2024-01-05')
    },
    {
      id: '11',
      tenantId: 'tenant1',
      sku: 'DRONE-011',
      name: 'Camera Drone 4K Pro',
      description: 'Professional quadcopter drone with 4K camera, 30-minute flight time, and GPS return-to-home.',
      category: 'Photography',
      brand: 'SkyTech',
      price: 899.99,
      compareAtPrice: 999.99,
      currency: 'USD',
      imageUrls: [
        'https://images.unsplash.com/photo-1508614589041-895b88991e3e?w=800',
        'https://images.unsplash.com/photo-1507582020474-9a35b7d455d9?w=800'
      ],
      tags: ['drone', 'camera', 'aerial', 'photography'],
      attributes: { camera: '4K 30fps', flight: '30 minutes', range: '4km', gps: 'Yes' },
      isActive: true,
      stockQuantity: 18,
      rating: { average: 4.5, count: 87 },
      createdAt: new Date('2024-02-20'),
      updatedAt: new Date('2024-02-20')
    },
    {
      id: '12',
      tenantId: 'tenant1',
      sku: 'CONSOLE-012',
      name: 'Gaming Console Next Gen',
      description: 'Next-generation gaming console with 4K 120fps gaming, ray tracing, and 1TB SSD storage.',
      category: 'Gaming',
      brand: 'GameGear',
      price: 499.99,
      compareAtPrice: 549.99,
      currency: 'USD',
      imageUrls: [
        'https://images.unsplash.com/photo-1486401899868-0e435ed85128?w=800',
        'https://images.unsplash.com/photo-1622297845775-5ff3fef71d13?w=800'
      ],
      tags: ['console', 'gaming', 'entertainment'],
      attributes: { resolution: '4K 120fps', storage: '1TB SSD', raytracing: 'Yes' },
      isActive: true,
      stockQuantity: 12,
      rating: { average: 4.8, count: 312 },
      createdAt: new Date('2024-01-12'),
      updatedAt: new Date('2024-01-12')
    },
    {
      id: '13',
      tenantId: 'tenant1',
      sku: 'EARBUDS-013',
      name: 'True Wireless Earbuds Pro',
      description: 'Premium wireless earbuds with active noise cancellation, 8-hour battery, and wireless charging.',
      category: 'Audio',
      brand: 'SoundWave',
      price: 199.99,
      compareAtPrice: 249.99,
      currency: 'USD',
      imageUrls: [
        'https://images.unsplash.com/photo-1590658268037-6bf12165a8df?w=800',
        'https://images.unsplash.com/photo-1606841837239-c5a1a4a07af7?w=800'
      ],
      tags: ['earbuds', 'wireless', 'noise-canceling'],
      attributes: { battery: '8 hours + 24h case', anc: 'Yes', charging: 'Wireless' },
      isActive: true,
      stockQuantity: 203,
      rating: { average: 4.6, count: 456 },
      createdAt: new Date('2024-02-08'),
      updatedAt: new Date('2024-02-08')
    },
    {
      id: '14',
      tenantId: 'tenant1',
      sku: 'ROUTER-014',
      name: 'WiFi 6 Gaming Router',
      description: 'Tri-band WiFi 6 router with 6000 Mbps speeds, gaming prioritization, and parental controls.',
      category: 'Networking',
      brand: 'NetPro',
      price: 299.99,
      compareAtPrice: 349.99,
      currency: 'USD',
      imageUrls: [
        'https://images.unsplash.com/photo-1606904825846-647eb07f5be2?w=800',
        'https://images.unsplash.com/photo-1614624532983-4ce03382d63d?w=800'
      ],
      tags: ['router', 'wifi', 'gaming', 'networking'],
      attributes: { speed: '6000 Mbps', bands: 'Tri-band', standard: 'WiFi 6' },
      isActive: true,
      stockQuantity: 47,
      rating: { average: 4.4, count: 98 },
      createdAt: new Date('2024-01-18'),
      updatedAt: new Date('2024-01-18')
    }
  ]);

  // Available filter options
  categories = computed(() => {
    const cats = new Set(this.allProducts().map(p => p.category));
    return Array.from(cats).sort();
  });

  brands = computed(() => {
    const brds = new Set(this.allProducts().map(p => p.brand));
    return Array.from(brds).sort();
  });

  // Sort options
  sortOptions: SelectOption[] = [
    { value: 'newest', label: 'Newest First' },
    { value: 'price-asc', label: 'Price: Low to High' },
    { value: 'price-desc', label: 'Price: High to Low' },
    { value: 'rating', label: 'Highest Rated' },
    { value: 'name', label: 'Name A-Z' }
  ];

  // Filtered and sorted products
  filteredProducts = computed(() => {
    let products = this.allProducts();

    // Apply search filter
    const search = this.searchQuery().toLowerCase();
    if (search) {
      products = products.filter(p =>
        p.name.toLowerCase().includes(search) ||
        p.description.toLowerCase().includes(search) ||
        p.brand.toLowerCase().includes(search) ||
        p.category.toLowerCase().includes(search) ||
        p.tags.some(tag => tag.toLowerCase().includes(search))
      );
    }

    // Apply category filter
    const category = this.selectedCategory();
    if (category) {
      products = products.filter(p => p.category === category);
    }

    // Apply brand filter
    const brand = this.selectedBrand();
    if (brand) {
      products = products.filter(p => p.brand === brand);
    }

    // Apply price range filter
    const range = this.priceRange();
    products = products.filter(p => p.price >= range.min && p.price <= range.max);

    // Apply sorting
    const sort = this.sortField();
    products = [...products].sort((a, b) => {
      switch (sort) {
        case 'price':
          return a.price - b.price;
        case 'rating':
          return (b.rating?.average || 0) - (a.rating?.average || 0);
        case 'newest':
          return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
        case 'name':
          return a.name.localeCompare(b.name);
        default:
          return 0;
      }
    });

    return products;
  });

  // Paginated products
  paginatedProducts = computed(() => {
    const products = this.filteredProducts();
    const page = this.currentPage();
    const perPage = this.itemsPerPage();
    const start = (page - 1) * perPage;
    const end = start + perPage;
    return products.slice(start, end);
  });

  // Total pages
  totalPages = computed(() => {
    return Math.ceil(this.filteredProducts().length / this.itemsPerPage());
  });

  // Total items
  totalItems = computed(() => this.filteredProducts().length);

  /**
   * Toggle view mode between grid and list
   */
  toggleViewMode(): void {
    this.viewMode.update(mode => mode === 'grid' ? 'list' : 'grid');
  }

  /**
   * Set view mode
   */
  setViewMode(mode: ViewMode): void {
    this.viewMode.set(mode);
  }

  /**
   * Handle search input
   */
  onSearch(query: string): void {
    this.searchQuery.set(query);
    this.currentPage.set(1); // Reset to first page on search
  }

  /**
   * Handle category filter change
   */
  onCategoryChange(category: string): void {
    this.selectedCategory.set(category);
    this.currentPage.set(1);
  }

  /**
   * Handle brand filter change
   */
  onBrandChange(brand: string): void {
    this.selectedBrand.set(brand);
    this.currentPage.set(1);
  }

  /**
   * Handle price range change
   */
  onPriceRangeChange(min: number, max: number): void {
    this.priceRange.set({ min, max });
    this.currentPage.set(1);
  }

  /**
   * Handle sort change
   */
  onSortChange(value: string): void {
    this.sortField.set(value as SortField);
  }

  /**
   * Handle page change
   */
  onPageChange(event: PageChangeEvent): void {
    this.currentPage.set(event.page);
    this.itemsPerPage.set(event.itemsPerPage);
    // Scroll to top on page change
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  /**
   * Handle product card click
   */
  onProductClick(product: Product): void {
    console.log('Product clicked:', product);
    // Navigate to product detail page
    // this.router.navigate(['/products', product.id]);
  }

  /**
   * Handle add to cart
   */
  onAddToCart(product: Product): void {
    console.log('Add to cart:', product);
    // Implement cart service call
  }

  /**
   * Handle wishlist toggle
   */
  onToggleWishlist(product: Product): void {
    console.log('Toggle wishlist:', product);
    // Implement wishlist service call
  }

  /**
   * Clear all filters
   */
  clearFilters(): void {
    this.searchQuery.set('');
    this.selectedCategory.set('');
    this.selectedBrand.set('');
    this.priceRange.set({ min: 0, max: 10000 });
    this.currentPage.set(1);
  }

  /**
   * Toggle filters visibility (for mobile)
   */
  toggleFilters(): void {
    this.showFilters.update(show => !show);
  }
}
