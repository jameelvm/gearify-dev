import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { OrderService } from '@core/services/order.service';
import { PaymentService } from '@core/services/payment.service';
import { AuthService } from '@features/auth/auth.service';
import { OrderDto } from '@core/models/order.model';
import { PaymentDto } from '@core/models/payment.model';

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './order-detail.component.html',
  styleUrls: ['./order-detail.component.scss']
})
export class OrderDetailComponent implements OnInit {
  private orderService = inject(OrderService);
  private paymentService = inject(PaymentService);
  private authService = inject(AuthService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  order = this.orderService.currentOrder;
  loading = this.orderService.loading;
  error = this.orderService.error;

  payment = signal<PaymentDto | null>(null);
  paymentLoading = signal(false);

  showCancelModal = signal(false);
  cancelReason = signal('');
  isCancelling = signal(false);

  ngOnInit(): void {
    const user = this.authService.user()?.user;

    if (!user) {
      this.router.navigate(['/auth/login']);
      return;
    }

    const orderId = this.route.snapshot.paramMap.get('id');
    if (orderId) {
      this.loadOrder(orderId);
    }
  }

  private loadOrder(orderId: string): void {
    this.orderService.getOrderById(orderId).subscribe({
      next: () => {
        this.loadPaymentDetails(orderId);
      },
      error: () => {
        // Error is handled by the service
      }
    });
  }

  private loadPaymentDetails(orderId: string): void {
    this.paymentLoading.set(true);
    this.paymentService.getPaymentByOrderId(orderId).subscribe({
      next: (payment) => {
        this.payment.set(payment);
        this.paymentLoading.set(false);
      },
      error: () => {
        this.paymentLoading.set(false);
      }
    });
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

  onBackToOrders(): void {
    this.router.navigate(['/account/orders']);
  }

  onCancelOrder(): void {
    this.showCancelModal.set(true);
  }

  onCloseCancelModal(): void {
    this.showCancelModal.set(false);
    this.cancelReason.set('');
  }

  onConfirmCancel(): void {
    const order = this.order();
    const reason = this.cancelReason();

    if (!order || !reason.trim()) return;

    this.isCancelling.set(true);

    this.orderService.cancelOrder(order.id, {
      reason: reason.trim(),
      cancelledBy: this.authService.user()?.user?.id
    }).subscribe({
      next: () => {
        this.isCancelling.set(false);
        this.showCancelModal.set(false);
        this.cancelReason.set('');
      },
      error: () => {
        this.isCancelling.set(false);
      }
    });
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

  formatStatus(status: string): string {
    return status.replace(/([A-Z])/g, ' $1').trim();
  }

  formatDate(dateString: string | null): string {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  canCancelOrder(): boolean {
    const order = this.order();
    if (!order) return false;

    const cancellableStatuses = ['pending', 'paymentprocessing', 'paid', 'processing'];
    return cancellableStatuses.includes(order.status.toLowerCase());
  }

  getProgressSteps(): { label: string; status: 'completed' | 'current' | 'pending' }[] {
    const order = this.order();
    if (!order) return [];

    const orderStatus = order.status.toLowerCase();
    const steps = [
      { label: 'Order Placed', key: 'pending' },
      { label: 'Payment', key: 'paid' },
      { label: 'Processing', key: 'processing' },
      { label: 'Shipped', key: 'shipped' },
      { label: 'Delivered', key: 'delivered' }
    ];

    const statusOrder = ['pending', 'paymentprocessing', 'paid', 'processing', 'shipped', 'delivered'];
    const currentIndex = statusOrder.indexOf(orderStatus);

    if (orderStatus === 'cancelled' || orderStatus === 'refunded' || orderStatus === 'paymentfailed') {
      return steps.map((step, index) => ({
        label: step.label,
        status: index === 0 ? 'completed' : 'pending' as 'completed' | 'current' | 'pending'
      }));
    }

    return steps.map((step, index) => {
      const stepIndex = statusOrder.indexOf(step.key);
      if (stepIndex < currentIndex || (stepIndex <= currentIndex && orderStatus === step.key)) {
        return { label: step.label, status: 'completed' as const };
      } else if (stepIndex === currentIndex || (currentIndex === 1 && index === 1)) {
        return { label: step.label, status: 'current' as const };
      }
      return { label: step.label, status: 'pending' as const };
    });
  }
}
