import { Injectable, inject, signal, computed } from '@angular/core';
import { Observable, of, tap, map, catchError } from 'rxjs';
import { HttpService } from './http.service';
import { Cart, CartItem, CartResponse, CartItemResponse, AddToCartRequest, UpdateCartItemRequest, GuestCartResponse } from '../models/cart.model';
import { API_CONFIG, STORAGE_KEYS } from '@shared/constants/api.constants';

/**
 * Shopping cart service with reactive state management
 * Integrates with backend cart API
 */
@Injectable({ providedIn: 'root' })
export class CartService {
  private http = inject(HttpService);

  // Signals for reactive UI
  private cartSignal = signal<Cart | null>(null);
  public cart = this.cartSignal.asReadonly();

  public itemCount = computed(() => {
    const cart = this.cartSignal();
    return cart?.itemCount ?? cart?.items.reduce((sum, item) => sum + item.quantity, 0) ?? 0;
  });

  public total = computed(() => this.cartSignal()?.total ?? 0);

  constructor() {
    this.loadCart();
  }

  /**
   * Load cart from API
   */
  loadCart(): void {
    const cartId = this.getOrCreateCartId();

    this.http.get<CartResponse>(`${API_CONFIG.ENDPOINTS.CART}/${cartId}`).pipe(
      map(response => this.mapResponseToCart(response)),
      tap(cart => this.cartSignal.set(cart)),
      catchError(err => {
        console.error('Failed to load cart:', err);
        // Return empty cart on error
        this.cartSignal.set(null);
        return of(null);
      })
    ).subscribe();
  }

  /**
   * Add item to cart
   */
  addToCart(request: AddToCartRequest): Observable<Cart> {
    const cartId = this.getOrCreateCartId();

    return this.http.post<CartResponse>(`${API_CONFIG.ENDPOINTS.CART}/${cartId}/items`, request).pipe(
      map(response => this.mapResponseToCart(response)),
      tap(cart => this.cartSignal.set(cart)),
      catchError(err => {
        console.error('Failed to add to cart:', err);
        throw err;
      })
    );
  }

  /**
   * Add to cart with product details (convenience method from product detail page)
   */
  addToCartWithProduct(
    product: { id: string; name: string; sku: string; price: number; imageUrl: string; brand: string },
    quantity: number
  ): Observable<Cart> {
    return this.addToCart({ productId: product.id, quantity });
  }

  /**
   * Update item quantity in cart
   */
  updateCartItem(productId: string, request: UpdateCartItemRequest): Observable<Cart> {
    const cartId = this.getOrCreateCartId();

    return this.http.put<CartResponse>(`${API_CONFIG.ENDPOINTS.CART}/${cartId}/items/${productId}`, request).pipe(
      map(response => this.mapResponseToCart(response)),
      tap(cart => this.cartSignal.set(cart)),
      catchError(err => {
        console.error('Failed to update cart item:', err);
        throw err;
      })
    );
  }

  /**
   * Remove item from cart
   */
  removeFromCart(productId: string): Observable<Cart> {
    const cartId = this.getOrCreateCartId();

    return this.http.delete<CartResponse>(`${API_CONFIG.ENDPOINTS.CART}/${cartId}/items/${productId}`).pipe(
      map(response => {
        if (response) {
          return this.mapResponseToCart(response);
        }
        // If no response, reload cart
        this.loadCart();
        return this.cartSignal()!;
      }),
      tap(cart => {
        if (cart) {
          this.cartSignal.set(cart);
        }
      }),
      catchError(err => {
        console.error('Failed to remove from cart:', err);
        throw err;
      })
    );
  }

  /**
   * Clear entire cart
   */
  clearCart(): Observable<void> {
    const cartId = this.getOrCreateCartId();

    return this.http.delete<void>(`${API_CONFIG.ENDPOINTS.CART}/${cartId}`).pipe(
      tap(() => this.cartSignal.set(null)),
      catchError(err => {
        console.error('Failed to clear cart:', err);
        throw err;
      })
    );
  }

  /**
   * Create a new guest cart and get the guest ID
   */
  createGuestCart(): Observable<string> {
    return this.http.post<GuestCartResponse>(`${API_CONFIG.ENDPOINTS.CART}/guest/new`, {}).pipe(
      map(response => response.guestId),
      tap(guestId => {
        localStorage.setItem(STORAGE_KEYS.CART_ID, guestId);
      }),
      catchError(err => {
        console.error('Failed to create guest cart:', err);
        // Fallback to local UUID generation
        const localId = this.generateUUID();
        localStorage.setItem(STORAGE_KEYS.CART_ID, localId);
        return of(localId);
      })
    );
  }

  // ============================================
  // Utility Methods
  // ============================================

  /**
   * Get or create cart ID (guest ID for unauthenticated users)
   */
  private getOrCreateCartId(): string {
    let cartId = localStorage.getItem(STORAGE_KEYS.CART_ID);
    if (!cartId) {
      // Generate a UUID locally for now
      // In production, you might want to call createGuestCart() first
      cartId = this.generateUUID();
      localStorage.setItem(STORAGE_KEYS.CART_ID, cartId);
    }
    return cartId;
  }

  /**
   * Generate a UUID v4
   */
  private generateUUID(): string {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
      const r = (Math.random() * 16) | 0;
      const v = c === 'x' ? r : (r & 0x3) | 0x8;
      return v.toString(16);
    });
  }

  /**
   * Map API response to frontend Cart model
   */
  private mapResponseToCart(response: CartResponse): Cart {
    return {
      id: response.id,
      userId: response.userId,
      items: response.items.map(item => this.mapItemResponseToCartItem(item)),
      subtotal: response.subtotal,
      total: response.total,
      currency: response.currency,
      itemCount: response.itemCount,
      createdAt: new Date(response.createdAt),
      updatedAt: new Date(response.updatedAt)
    };
  }

  /**
   * Map API item response to frontend CartItem model
   */
  private mapItemResponseToCartItem(item: CartItemResponse): CartItem {
    return {
      productId: item.productId,
      product: {
        name: item.productName,
        sku: item.sku,
        price: item.unitPrice,
        imageUrl: item.imageUrl
      },
      quantity: item.quantity,
      subtotal: item.lineTotal
    };
  }
}
