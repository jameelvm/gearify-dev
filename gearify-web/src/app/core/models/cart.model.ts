/**
 * Shopping cart models
 */
export interface CartItem {
  id: string;
  productId: string;
  product: {
    name: string;
    sku: string;
    price: number;
    imageUrl: string;
    brand: string;
  };
  quantity: number;
  selectedVariants?: Record<string, string>;
  subtotal: number;
}

export interface Cart {
  id: string;
  tenantId: string;
  userId?: string;
  sessionId: string;
  items: CartItem[];
  subtotal: number;
  tax: number;
  shipping: number;
  discount: number;
  total: number;
  promoCode?: string;
  currency: string;
  createdAt: Date;
  updatedAt: Date;
}

export interface AddToCartRequest {
  productId: string;
  quantity: number;
  variants?: Record<string, string>;
}

export interface UpdateCartItemRequest {
  itemId: string;
  quantity: number;
}
