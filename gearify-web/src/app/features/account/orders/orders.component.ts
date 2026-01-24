import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { OrderService } from '@core/services/order.service';
import { PaymentService } from '@core/services/payment.service';
import { AuthService } from '@features/auth/auth.service';
import { OrderSummaryDto } from '@core/models/order.model';
import { PaymentDto } from '@core/models/payment.model';

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './orders.component.html',
  styleUrls: ['./orders.component.scss']
})
export class OrdersComponent implements OnInit {
  private orderService = inject(OrderService);
  private paymentService = inject(PaymentService);
  private authService = inject(AuthService);
  private router = inject(Router);

  orders = this.orderService.orders;
  loading = this.orderService.loading;
  error = this.orderService.error;

  // Payment details map: orderId -> PaymentDto
  paymentDetails = signal<Record<string, PaymentDto>>({});
  // Track which orders have payment section expanded
  expandedPayments = signal<Record<string, boolean>>({});
  // Track loading state per order
  paymentLoading = signal<Record<string, boolean>>({});

  // Filter state
  statusFilter = signal<string>('all');

  filteredOrders = computed(() => {
    const allOrders = this.orders();
    const filter = this.statusFilter();

    if (filter === 'all') {
      return allOrders;
    }
    return allOrders.filter(order => order.status.toLowerCase() === filter.toLowerCase());
  });

  ngOnInit(): void {
    const user = this.authService.user()?.user;

    if (!user) {
      this.router.navigate(['/auth/login']);
      return;
    }

    this.loadOrders(user.id);
  }

  private loadOrders(userId: string): void {
    this.orderService.getOrdersByUser(userId).subscribe();
  }

  togglePaymentDetails(orderId: string, event: Event): void {
    event.stopPropagation();
    const isExpanded = this.expandedPayments()[orderId];

    if (isExpanded) {
      this.expandedPayments.update(map => ({ ...map, [orderId]: false }));
      return;
    }

    this.expandedPayments.update(map => ({ ...map, [orderId]: true }));

    // Fetch if not already loaded
    if (!this.paymentDetails()[orderId]) {
      this.paymentLoading.update(map => ({ ...map, [orderId]: true }));
      this.paymentService.getPaymentByOrderId(orderId).subscribe({
        next: (payment) => {
          this.paymentDetails.update(map => ({ ...map, [orderId]: payment }));
          this.paymentLoading.update(map => ({ ...map, [orderId]: false }));
        },
        error: () => {
          this.paymentLoading.update(map => ({ ...map, [orderId]: false }));
        }
      });
    }
  }

  isPaymentExpanded(orderId: string): boolean {
    return !!this.expandedPayments()[orderId];
  }

  isPaymentLoading(orderId: string): boolean {
    return !!this.paymentLoading()[orderId];
  }

  getPayment(orderId: string): PaymentDto | undefined {
    return this.paymentDetails()[orderId];
  }

  onViewOrder(orderId: string): void {
    this.router.navigate(['/account/orders', orderId]);
  }

  onFilterChange(status: string): void {
    this.statusFilter.set(status);
  }

  getStatusClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'pending':
        return 'status-pending';
      case 'paymentprocessing':
        return 'status-processing';
      case 'paid':
        return 'status-paid';
      case 'processing':
        return 'status-processing';
      case 'shipped':
        return 'status-shipped';
      case 'delivered':
        return 'status-delivered';
      case 'cancelled':
        return 'status-cancelled';
      case 'refunded':
        return 'status-refunded';
      case 'paymentfailed':
        return 'status-failed';
      default:
        return 'status-default';
    }
  }

  getPaymentStatusClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'succeeded':
        return 'payment-succeeded';
      case 'pending':
      case 'processing':
        return 'payment-pending';
      case 'failed':
      case 'cancelled':
        return 'payment-failed';
      case 'refunded':
      case 'partiallyrefunded':
        return 'payment-refunded';
      default:
        return 'payment-default';
    }
  }

  formatStatus(status: string): string {
    // Convert PascalCase to readable format
    return status.replace(/([A-Z])/g, ' $1').trim();
  }

  formatDate(dateString: string): string {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }
}
