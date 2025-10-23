import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ButtonComponent,
  BadgeComponent,
  SpinnerComponent,
  InputComponent,
  SelectComponent,
  CheckboxComponent,
  ProductCardComponent,
  PriceTagComponent,
  RatingComponent,
  ModalComponent,
  BreadcrumbComponent,
  PaginationComponent,
  ImageGalleryComponent,
  BrandBarComponent
} from '@app/ui-kit/components';
import { ThemeService } from '@core/services/theme.service';
import { Product } from '@core/models/product.model';

/**
 * UI Showcase Component
 * Demonstrates all UI Kit components in action
 */
@Component({
  selector: 'app-ui-showcase',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    ButtonComponent,
    BadgeComponent,
    SpinnerComponent,
    InputComponent,
    SelectComponent,
    CheckboxComponent,
    ProductCardComponent,
    PriceTagComponent,
    RatingComponent,
    ModalComponent,
    BreadcrumbComponent,
    PaginationComponent,
    ImageGalleryComponent,
    BrandBarComponent
  ],
  templateUrl: './ui-showcase.component.html',
  styleUrl: './ui-showcase.component.scss'
})
export class UiShowcaseComponent {
  // Form for testing form components
  testForm = new FormGroup({
    email: new FormControl('', [Validators.required, Validators.email]),
    country: new FormControl(''),
    acceptTerms: new FormControl(false)
  });

  // Sample data
  countries = [
    { value: 'us', label: 'United States' },
    { value: 'uk', label: 'United Kingdom' },
    { value: 'ca', label: 'Canada' },
    { value: 'au', label: 'Australia' }
  ];

  breadcrumbs = [
    { label: 'Home', url: '/' },
    { label: 'Products', url: '/products' },
    { label: 'Electronics', url: '/products/electronics' },
    { label: 'Laptop' }
  ];

  sampleProduct: Product = {
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
    imageUrls: ['https://via.placeholder.com/300x300?text=Laptop'],
    tags: ['laptop', 'electronics', 'premium'],
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
  };

  galleryImages = [
    { url: 'https://via.placeholder.com/600x400?text=Image+1', alt: 'Image 1' },
    { url: 'https://via.placeholder.com/600x400?text=Image+2', alt: 'Image 2' },
    { url: 'https://via.placeholder.com/600x400?text=Image+3', alt: 'Image 3' },
    { url: 'https://via.placeholder.com/600x400?text=Image+4', alt: 'Image 4' }
  ];

  brands = [
    { id: '1', name: 'Nike', logoUrl: 'https://via.placeholder.com/100x50?text=Nike' },
    { id: '2', name: 'Adidas', logoUrl: 'https://via.placeholder.com/100x50?text=Adidas' },
    { id: '3', name: 'Puma', logoUrl: 'https://via.placeholder.com/100x50?text=Puma' },
    { id: '4', name: 'Reebok', logoUrl: 'https://via.placeholder.com/100x50?text=Reebok' },
    { id: '5', name: 'Under Armour', logoUrl: 'https://via.placeholder.com/100x50?text=UnderArmour' }
  ];

  // State
  isLoading = signal(false);
  showModal = signal(false);
  currentPage = signal(1);
  userRating = signal(0);

  constructor(public themeService: ThemeService) {}

  simulateLoading() {
    this.isLoading.set(true);
    setTimeout(() => this.isLoading.set(false), 2000);
  }

  handleRatingChange(rating: number) {
    this.userRating.set(rating);
    console.log('Rating changed to:', rating);
  }

  handlePageChange(event: any) {
    this.currentPage.set(event.page);
    console.log('Page changed to:', event);
  }

  handleFormSubmit() {
    if (this.testForm.valid) {
      console.log('Form submitted:', this.testForm.value);
    } else {
      console.log('Form is invalid');
    }
  }

  logProductClick(event: any) {
    console.log('Product clicked', event);
  }

  logAddToCart(event: any) {
    console.log('Add to cart', event);
  }

  logWishlist(event: any) {
    console.log('Toggle wishlist', event);
  }

  logBreadcrumb(event: any) {
    console.log('Breadcrumb clicked', event);
  }

  logImage(event: any) {
    console.log('Image clicked', event);
  }

  logBrand(event: any) {
    console.log('Brand clicked', event);
  }

  get emailError(): string | undefined {
    const control = this.testForm.get('email');
    if (control?.hasError('required') && control.touched) {
      return 'Email is required';
    }
    if (control?.hasError('email') && control.touched) {
      return 'Invalid email format';
    }
    return undefined;
  }
}
