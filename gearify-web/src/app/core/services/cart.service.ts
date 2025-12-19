import { Injectable, inject, signal, computed } from '@angular/core';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { HttpService } from './http.service';
import { Cart, CartItem, AddToCartRequest, UpdateCartItemRequest } from '../models/cart.model';
import { API_CONFIG, STORAGE_KEYS } from '@shared/constants/api.constants';

/**
 * Shopping cart service with reactive state management
 */
@Injectable({ providedIn: 'root' })
export class CartService {
  private http = inject(HttpService);

  private cartSubject = new BehaviorSubject<Cart | null>(null);
  public cart$ = this.cartSubject.asObservable();

  // Signals for reactive UI
  private cartSignal = signal<Cart | null>(null);
  public cart = this.cartSignal.asReadonly();

  public itemCount = computed(() => {
    const cart = this.cartSignal();
    return cart?.items.reduce((sum, item) => sum + item.quantity, 0) ?? 0;
  });

  public total = computed(() => this.cartSignal()?.total ?? 0);

  constructor() {
    this.loadCart();
  }

  loadCart(): void {
    const sessionId = this.getOrCreateSessionId();
    this.http.get<Cart>(`${API_CONFIG.ENDPOINTS.CART}/${sessionId}`).pipe(
      tap(cart => {
        this.cartSubject.next(cart);
        this.cartSignal.set(cart);
      })
    ).subscribe();
  }

  addToCart(request: AddToCartRequest): Observable<Cart> {
    const sessionId = this.getOrCreateSessionId();
    return this.http.post<Cart>(`${API_CONFIG.ENDPOINTS.CART}/${sessionId}/items`, request).pipe(
      tap(cart => {
        this.cartSubject.next(cart);
        this.cartSignal.set(cart);
      })
    );
  }

  updateCartItem(itemId: string, request: UpdateCartItemRequest): Observable<Cart> {
    const sessionId = this.getOrCreateSessionId();
    return this.http.put<Cart>(`${API_CONFIG.ENDPOINTS.CART}/${sessionId}/items/${itemId}`, request).pipe(
      tap(cart => {
        this.cartSubject.next(cart);
        this.cartSignal.set(cart);
      })
    );
  }

  removeFromCart(itemId: string): Observable<Cart> {
    const sessionId = this.getOrCreateSessionId();
    return this.http.delete<Cart>(`${API_CONFIG.ENDPOINTS.CART}/${sessionId}/items/${itemId}`).pipe(
      tap(cart => {
        this.cartSubject.next(cart);
        this.cartSignal.set(cart);
      })
    );
  }

  clearCart(): Observable<void> {
    const sessionId = this.getOrCreateSessionId();
    return this.http.delete<void>(`${API_CONFIG.ENDPOINTS.CART}/${sessionId}`).pipe(
      tap(() => {
        this.cartSubject.next(null);
        this.cartSignal.set(null);
      })
    );
  }

  private getOrCreateSessionId(): string {
    let sessionId = localStorage.getItem(STORAGE_KEYS.SESSION_ID);
    if (!sessionId) {
      sessionId = this.generateSessionId();
      localStorage.setItem(STORAGE_KEYS.SESSION_ID, sessionId);
    }
    return sessionId;
  }

  private generateSessionId(): string {
    return `session_${Date.now()}_${Math.random().toString(36).substring(2, 15)}`;
  }
}
