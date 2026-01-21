import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { HttpService } from './http.service';
import { API_CONFIG } from '@shared/constants/api.constants';
import {
  OrderDto,
  OrderSummaryDto,
  OrderListResponse,
  CreateOrderRequest,
  CancelOrderRequest,
  UpdateOrderStatusRequest
} from '@core/models/order.model';

/**
 * Order Service
 * Manages order operations - creating orders, retrieving order history, cancellations
 */
@Injectable({ providedIn: 'root' })
export class OrderService {
  private http = inject(HttpService);

  private ordersSignal = signal<OrderSummaryDto[]>([]);
  private currentOrderSignal = signal<OrderDto | null>(null);
  private loadingSignal = signal(false);
  private errorSignal = signal<string | null>(null);

  // Read-only access
  readonly orders = this.ordersSignal.asReadonly();
  readonly currentOrder = this.currentOrderSignal.asReadonly();
  readonly loading = this.loadingSignal.asReadonly();
  readonly error = this.errorSignal.asReadonly();

  /**
   * Create a new order
   */
  createOrder(request: CreateOrderRequest): Observable<OrderDto> {
    this.loadingSignal.set(true);
    this.errorSignal.set(null);

    return this.http.post<OrderDto>(API_CONFIG.ENDPOINTS.ORDERS, request).pipe(
      tap({
        next: (order) => {
          this.currentOrderSignal.set(order);
          this.loadingSignal.set(false);
        },
        error: (err) => {
          this.errorSignal.set(err.error?.detail || err.message || 'Failed to create order');
          this.loadingSignal.set(false);
        }
      })
    );
  }

  /**
   * Get order by ID
   */
  getOrderById(orderId: string): Observable<OrderDto> {
    this.loadingSignal.set(true);
    this.errorSignal.set(null);

    return this.http.get<OrderDto>(API_CONFIG.ENDPOINTS.ORDER_BY_ID(orderId)).pipe(
      tap({
        next: (order) => {
          this.currentOrderSignal.set(order);
          this.loadingSignal.set(false);
        },
        error: (err) => {
          this.errorSignal.set(err.error?.detail || 'Order not found');
          this.loadingSignal.set(false);
        }
      })
    );
  }

  /**
   * Get order by order number
   */
  getOrderByNumber(orderNumber: string): Observable<OrderDto> {
    this.loadingSignal.set(true);
    this.errorSignal.set(null);

    return this.http.get<OrderDto>(API_CONFIG.ENDPOINTS.ORDER_BY_NUMBER(orderNumber)).pipe(
      tap({
        next: (order) => {
          this.currentOrderSignal.set(order);
          this.loadingSignal.set(false);
        },
        error: (err) => {
          this.errorSignal.set(err.error?.detail || 'Order not found');
          this.loadingSignal.set(false);
        }
      })
    );
  }

  /**
   * Get all orders for a user
   */
  getOrdersByUser(userId: string): Observable<OrderListResponse> {
    this.loadingSignal.set(true);
    this.errorSignal.set(null);

    return this.http.get<OrderListResponse>(`${API_CONFIG.ENDPOINTS.ORDERS}?userId=${userId}`).pipe(
      tap({
        next: (response) => {
          this.ordersSignal.set(response.orders);
          this.loadingSignal.set(false);
        },
        error: (err) => {
          this.errorSignal.set(err.error?.detail || 'Failed to load orders');
          this.loadingSignal.set(false);
        }
      })
    );
  }

  /**
   * Cancel an order
   */
  cancelOrder(orderId: string, request: CancelOrderRequest): Observable<OrderDto> {
    this.loadingSignal.set(true);
    this.errorSignal.set(null);

    return this.http.post<OrderDto>(API_CONFIG.ENDPOINTS.CANCEL_ORDER(orderId), request).pipe(
      tap({
        next: (order) => {
          this.currentOrderSignal.set(order);
          // Update in list if present
          this.ordersSignal.update(orders =>
            orders.map(o => o.id === orderId ? { ...o, status: order.status } : o)
          );
          this.loadingSignal.set(false);
        },
        error: (err) => {
          this.errorSignal.set(err.error?.detail || 'Failed to cancel order');
          this.loadingSignal.set(false);
        }
      })
    );
  }

  /**
   * Update order status (admin/internal use)
   */
  updateOrderStatus(orderId: string, request: UpdateOrderStatusRequest): Observable<OrderDto> {
    this.loadingSignal.set(true);
    this.errorSignal.set(null);

    return this.http.patch<OrderDto>(API_CONFIG.ENDPOINTS.UPDATE_ORDER_STATUS(orderId), request).pipe(
      tap({
        next: (order) => {
          this.currentOrderSignal.set(order);
          // Update in list if present
          this.ordersSignal.update(orders =>
            orders.map(o => o.id === orderId ? { ...o, status: order.status } : o)
          );
          this.loadingSignal.set(false);
        },
        error: (err) => {
          this.errorSignal.set(err.error?.detail || 'Failed to update order status');
          this.loadingSignal.set(false);
        }
      })
    );
  }

  /**
   * Clear the current order (e.g., after successful checkout)
   */
  clearCurrentOrder(): void {
    this.currentOrderSignal.set(null);
    this.errorSignal.set(null);
  }

  /**
   * Clear all order state (e.g., on logout)
   */
  clearOrders(): void {
    this.ordersSignal.set([]);
    this.currentOrderSignal.set(null);
    this.errorSignal.set(null);
  }
}
