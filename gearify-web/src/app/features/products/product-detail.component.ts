import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Product } from '@core/models/product.model';
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
export class ProductDetailComponent {
  // Active tab
  activeTab = signal<TabType>('description');

  // Quantity selector
  quantity = signal<number>(1);

  // Wishlist state
  isWishlisted = signal<boolean>(false);

  // Mock product data (in real app, this would come from a service/route param)
  product = signal<Product>({
    id: '1',
    tenantId: 'tenant1',
    sku: 'LAPTOP-001',
    name: 'UltraBook Pro 15',
    description: `
      The UltraBook Pro 15 is a powerful and versatile laptop designed for professionals and content creators who demand the best.

      Featuring a stunning 15-inch Retina display with True Tone technology, every image comes to life with vibrant colors and incredible detail. The Intel Core i7 processor delivers exceptional performance for demanding tasks like video editing, 3D rendering, and software development.

      With 16GB of high-speed RAM and a 512GB SSD, you'll have plenty of memory and storage for all your projects and files. The all-day battery life ensures you can work unplugged from morning to night.

      Key Features:
      • 15-inch Retina display with True Tone (2880 x 1800 resolution)
      • Intel Core i7 11th Gen processor (up to 4.7 GHz)
      • 16GB DDR4 RAM
      • 512GB PCIe SSD
      • Intel Iris Xe Graphics
      • Thunderbolt 4 ports
      • Wi-Fi 6 and Bluetooth 5.0
      • Backlit keyboard
      • Up to 15 hours battery life
      • Premium aluminum unibody design

      Perfect for:
      • Content creators and video editors
      • Software developers and engineers
      • Business professionals
      • Students and educators
      • Anyone who needs a reliable, powerful laptop
    `,
    category: 'Electronics',
    brand: 'TechPro',
    price: 1299.99,
    compareAtPrice: 1499.99,
    currency: 'USD',
    imageUrls: [
      'https://images.unsplash.com/photo-1496181133206-80ce9b88a853?w=800',
      'https://images.unsplash.com/photo-1517336714731-489689fd1ca8?w=800',
      'https://images.unsplash.com/photo-1588872657578-7efd1f1555ed?w=800',
      'https://images.unsplash.com/photo-1531297484001-80022131f5a1?w=800'
    ],
    tags: ['laptop', 'computer', 'portable', 'professional'],
    attributes: {
      processor: 'Intel Core i7 11th Gen',
      ram: '16GB DDR4',
      storage: '512GB SSD',
      display: '15-inch Retina',
      graphics: 'Intel Iris Xe',
      battery: 'Up to 15 hours',
      weight: '1.8 kg',
      color: 'Space Gray',
      warranty: '2 years'
    },
    isActive: true,
    stockQuantity: 45,
    rating: { average: 4.5, count: 127 },
    createdAt: new Date('2024-01-15'),
    updatedAt: new Date('2024-01-15')
  });

  // Mock reviews data
  reviews = signal<Review[]>([
    {
      id: '1',
      userId: 'user1',
      userName: 'John Smith',
      userAvatar: 'https://i.pravatar.cc/150?u=user1',
      rating: 5,
      title: 'Excellent laptop for professional work',
      comment: 'I\'ve been using this laptop for 3 months now and it\'s been fantastic. The performance is excellent, the display is beautiful, and the battery life is amazing. Highly recommend for anyone looking for a premium laptop.',
      createdAt: new Date('2024-02-20'),
      verified: true,
      helpful: 24
    },
    {
      id: '2',
      userId: 'user2',
      userName: 'Sarah Johnson',
      userAvatar: 'https://i.pravatar.cc/150?u=user2',
      rating: 4,
      title: 'Great performance but a bit pricey',
      comment: 'This laptop delivers on performance and build quality. The screen is gorgeous and the keyboard is comfortable for long typing sessions. My only complaint is the price, but you get what you pay for.',
      createdAt: new Date('2024-02-15'),
      verified: true,
      helpful: 18
    },
    {
      id: '3',
      userId: 'user3',
      userName: 'Michael Chen',
      userAvatar: 'https://i.pravatar.cc/150?u=user3',
      rating: 5,
      title: 'Perfect for video editing',
      comment: 'As a video editor, I need a powerful machine that can handle 4K footage. This laptop does it with ease. Export times are fast, and I can work with multiple applications open without any lag.',
      createdAt: new Date('2024-02-10'),
      verified: true,
      helpful: 32
    },
    {
      id: '4',
      userId: 'user4',
      userName: 'Emily Davis',
      userAvatar: 'https://i.pravatar.cc/150?u=user4',
      rating: 4,
      title: 'Solid choice for professionals',
      comment: 'Very happy with this purchase. The build quality is excellent, and the performance is more than enough for my daily tasks. Battery life easily lasts a full work day.',
      createdAt: new Date('2024-01-28'),
      verified: false,
      helpful: 12
    },
    {
      id: '5',
      userId: 'user5',
      userName: 'David Wilson',
      userAvatar: 'https://i.pravatar.cc/150?u=user5',
      rating: 5,
      title: 'Best laptop I\'ve ever owned',
      comment: 'Everything about this laptop is premium. The display is stunning, the trackpad is incredibly smooth, and the speakers are surprisingly good. Worth every penny!',
      createdAt: new Date('2024-01-22'),
      verified: true,
      helpful: 45
    }
  ]);

  // Mock related products
  relatedProducts = signal<Product[]>([
    {
      id: '2',
      tenantId: 'tenant1',
      sku: 'LAPTOP-002',
      name: 'UltraBook Air 13',
      description: 'Lightweight laptop perfect for everyday tasks',
      category: 'Electronics',
      brand: 'TechPro',
      price: 999.99,
      compareAtPrice: 1099.99,
      currency: 'USD',
      imageUrls: ['https://images.unsplash.com/photo-1517336714731-489689fd1ca8?w=800'],
      tags: ['laptop', 'portable'],
      attributes: { processor: 'Intel Core i5', ram: '8GB', storage: '256GB SSD' },
      isActive: true,
      stockQuantity: 32,
      rating: { average: 4.3, count: 89 },
      createdAt: new Date('2024-01-20'),
      updatedAt: new Date('2024-01-20')
    },
    {
      id: '3',
      tenantId: 'tenant1',
      sku: 'TABLET-005',
      name: 'Pro Tablet 12.9',
      description: 'Professional tablet for creative work',
      category: 'Electronics',
      brand: 'TechPro',
      price: 1099.99,
      compareAtPrice: 1199.99,
      currency: 'USD',
      imageUrls: ['https://images.unsplash.com/photo-1561154464-82e9adf32764?w=800'],
      tags: ['tablet', 'creative'],
      attributes: { screen: '12.9-inch', chip: 'M2', storage: '256GB' },
      isActive: true,
      stockQuantity: 18,
      rating: { average: 4.8, count: 92 },
      createdAt: new Date('2024-02-05'),
      updatedAt: new Date('2024-02-05')
    },
    {
      id: '4',
      tenantId: 'tenant1',
      sku: 'MONITOR-010',
      name: '4K Monitor 27"',
      description: '27-inch 4K monitor with HDR',
      category: 'Electronics',
      brand: 'TechPro',
      price: 449.99,
      compareAtPrice: 549.99,
      currency: 'USD',
      imageUrls: ['https://images.unsplash.com/photo-1527443224154-c4a3942d3acf?w=800'],
      tags: ['monitor', '4k'],
      attributes: { size: '27-inch', resolution: '4K', panel: 'IPS' },
      isActive: true,
      stockQuantity: 28,
      rating: { average: 4.7, count: 145 },
      createdAt: new Date('2024-01-05'),
      updatedAt: new Date('2024-01-05')
    },
    {
      id: '5',
      tenantId: 'tenant1',
      sku: 'MOUSE-009',
      name: 'Wireless Mouse Pro',
      description: 'Ergonomic wireless mouse',
      category: 'Accessories',
      brand: 'TechPro',
      price: 79.99,
      compareAtPrice: 99.99,
      currency: 'USD',
      imageUrls: ['https://images.unsplash.com/photo-1527814050087-3793815479db?w=800'],
      tags: ['mouse', 'wireless'],
      attributes: { dpi: '16000', battery: '70 hours' },
      isActive: true,
      stockQuantity: 156,
      rating: { average: 4.6, count: 234 },
      createdAt: new Date('2024-02-15'),
      updatedAt: new Date('2024-02-15')
    }
  ]);

  // Computed gallery images
  galleryImages = computed<GalleryImage[]>(() => {
    return this.product().imageUrls.map((url, index) => ({
      url,
      alt: `${this.product().name} - Image ${index + 1}`,
      thumbnail: url
    }));
  });

  // Computed properties
  hasDiscount = computed(() => {
    const prod = this.product();
    return prod.compareAtPrice > prod.price;
  });

  discountPercent = computed(() => {
    const prod = this.product();
    if (!this.hasDiscount()) return 0;
    return Math.round(((prod.compareAtPrice - prod.price) / prod.compareAtPrice) * 100);
  });

  isOutOfStock = computed(() => {
    const stock = this.product().stockQuantity;
    return stock !== undefined && stock <= 0;
  });

  hasLowStock = computed(() => {
    const stock = this.product().stockQuantity;
    return stock !== undefined && stock > 0 && stock < 10;
  });

  // Average rating
  averageRating = computed(() => {
    const reviewList = this.reviews();
    if (reviewList.length === 0) return 0;
    const sum = reviewList.reduce((acc, review) => acc + review.rating, 0);
    return sum / reviewList.length;
  });

  // Rating distribution
  ratingDistribution = computed(() => {
    const reviewList = this.reviews();
    const distribution = { 5: 0, 4: 0, 3: 0, 2: 0, 1: 0 };

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
    const stock = this.product().stockQuantity || 0;
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
    console.log('Related product clicked:', product);
    // Navigate to product detail page
    // this.router.navigate(['/products', product.id]);
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
