/**
 * Product model matching backend DTO
 */
export interface Product {
  id: string;
  tenantId: string;
  sku: string;
  name: string;
  description: string;

  // Catalog hierarchy fields
  department?: string;
  departmentSlug?: string;
  category: string;
  categorySlug?: string;
  subcategory?: string;
  subcategorySlug?: string;

  // Brand fields
  brand: string;
  brandSlug?: string;

  // Pricing fields
  price: number;                    // Current selling price
  compareAtPrice: number;           // Original price for strikethrough
  discountPercentage?: number;      // Discount percentage (e.g., 20 for "20% off")
  offerBadge?: string;              // Offer label (e.g., "SALE", "20% OFF", "HOT DEAL")
  currency: string;

  // Image fields
  thumbnailUrl?: string;            // Primary thumbnail URL for list views (updated by Media Service)
  imageUrls: string[];              // Legacy field for backward compatibility

  tags: string[];
  attributes: Record<string, string>;
  isActive: boolean;
  stockQuantity?: number;

  // Rating fields (matches API response)
  ratingAverage?: number;               // Average rating (e.g., 4.5)
  ratingCount?: number;                 // Number of reviews (e.g., 127)

  // Deprecated: kept for backward compatibility
  rating?: ProductRating;

  createdAt: Date;
  updatedAt: Date;
}

export interface ProductRating {
  average: number;
  count: number;
}

export interface ProductVariant {
  id: string;
  name: string;
  options: ProductVariantOption[];
}

export interface ProductVariantOption {
  id: string;
  value: string;
  priceAdjustment?: number;
  inStock: boolean;
}

export interface ProductFilter {
  category?: string;
  brand?: string;
  minPrice?: number;
  maxPrice?: number;
  tags?: string[];
  searchTerm?: string;
  inStockOnly?: boolean;
}

export interface ProductSortOption {
  field: 'price' | 'name' | 'createdAt' | 'rating';
  direction: 'asc' | 'desc';
}
