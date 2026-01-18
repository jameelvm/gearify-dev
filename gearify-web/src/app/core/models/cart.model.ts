/**
 * Shopping cart models - aligned with backend API
 */

// Backend API response models
export interface CartItemResponse {
  productId: string;
  productName: string;
  sku: string;
  imageUrl: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface CartResponse {
  id: string;
  userId: string;
  items: CartItemResponse[];
  subtotal: number;
  total: number;
  currency: string;
  itemCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface GuestCartResponse {
  guestId: string;
}

// Frontend display models (mapped from API response)
export interface CartItem {
  productId: string;
  product: {
    name: string;
    sku: string;
    price: number;
    imageUrl: string;
    brand?: string;
  };
  quantity: number;
  subtotal: number;
}

export interface Cart {
  id: string;
  userId: string;
  items: CartItem[];
  subtotal: number;
  total: number;
  currency: string;
  itemCount: number;
  createdAt: Date;
  updatedAt: Date;
}

// Request models
export interface AddToCartRequest {
  productId: string;
  quantity: number;
}

export interface UpdateCartItemRequest {
  quantity: number;
}

// Backend expects PascalCase and enum as number
export interface MergeCartRequest {
  GuestCartId: string;
  UserId: string;
  Strategy: MergeStrategy;
}

export enum MergeStrategy {
  Combine = 0,
  ReplaceWithGuest = 1,
  KeepUser = 2
}
