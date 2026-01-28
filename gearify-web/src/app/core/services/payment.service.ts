import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { HttpService } from './http.service';
import { API_CONFIG } from '@shared/constants/api.constants';
import {
  PaymentDto,
  PaymentListDto,
  ProcessRefundRequest,
  RefundDto
} from '@core/models/payment.model';

/**
 * Payment Service
 * Manages payment processing operations
 */
@Injectable({ providedIn: 'root' })
export class PaymentService {
  private http = inject(HttpService);

  private currentPaymentSignal = signal<PaymentDto | null>(null);
  private loadingSignal = signal(false);
  private errorSignal = signal<string | null>(null);

  // Read-only access
  readonly currentPayment = this.currentPaymentSignal.asReadonly();
  readonly loading = this.loadingSignal.asReadonly();
  readonly error = this.errorSignal.asReadonly();

  /**
   * Get payment by ID
   */
  getPaymentById(paymentId: string): Observable<PaymentDto> {
    this.loadingSignal.set(true);
    this.errorSignal.set(null);

    return this.http.get<PaymentDto>(API_CONFIG.ENDPOINTS.PAYMENT_BY_ID(paymentId)).pipe(
      tap({
        next: (payment) => {
          this.currentPaymentSignal.set(payment);
          this.loadingSignal.set(false);
        },
        error: (err) => {
          this.errorSignal.set(err.error?.detail || 'Payment not found');
          this.loadingSignal.set(false);
        }
      })
    );
  }

  /**
   * Get payment for an order
   */
  getPaymentByOrderId(orderId: string): Observable<PaymentDto> {
    this.loadingSignal.set(true);
    this.errorSignal.set(null);

    return this.http.get<PaymentDto>(API_CONFIG.ENDPOINTS.PAYMENT_BY_ORDER(orderId)).pipe(
      tap({
        next: (payment) => {
          this.currentPaymentSignal.set(payment);
          this.loadingSignal.set(false);
        },
        error: (err) => {
          this.errorSignal.set(err.error?.detail || 'No payment found for this order');
          this.loadingSignal.set(false);
        }
      })
    );
  }

  /**
   * Get all payments for a user
   */
  getPaymentsByUser(userId: string, page: number = 1, pageSize: number = 20): Observable<PaymentListDto> {
    this.loadingSignal.set(true);
    this.errorSignal.set(null);

    const url = `${API_CONFIG.ENDPOINTS.PAYMENTS}?userId=${userId}&page=${page}&pageSize=${pageSize}`;

    return this.http.get<PaymentListDto>(url).pipe(
      tap({
        next: () => {
          this.loadingSignal.set(false);
        },
        error: (err) => {
          this.errorSignal.set(err.error?.detail || 'Failed to load payments');
          this.loadingSignal.set(false);
        }
      })
    );
  }

  /**
   * Process a refund
   */
  processRefund(paymentId: string, request: ProcessRefundRequest): Observable<RefundDto> {
    this.loadingSignal.set(true);
    this.errorSignal.set(null);

    return this.http.post<RefundDto>(API_CONFIG.ENDPOINTS.REFUND_PAYMENT(paymentId), request).pipe(
      tap({
        next: () => {
          this.loadingSignal.set(false);
          // Reload payment to get updated refund list
          this.getPaymentById(paymentId).subscribe();
        },
        error: (err) => {
          this.errorSignal.set(err.error?.detail || 'Refund processing failed');
          this.loadingSignal.set(false);
        }
      })
    );
  }

  /**
   * Clear the current payment state
   */
  clearCurrentPayment(): void {
    this.currentPaymentSignal.set(null);
    this.errorSignal.set(null);
  }

  /**
   * Clear all payment state (e.g., on logout)
   */
  clearPayments(): void {
    this.currentPaymentSignal.set(null);
    this.errorSignal.set(null);
  }
}
