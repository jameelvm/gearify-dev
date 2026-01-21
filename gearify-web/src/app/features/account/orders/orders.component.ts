import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { OrderService } from '@core/services/order.service';
import { AuthService } from '@features/auth/auth.service';
import { OrderSummaryDto } from '@core/models/order.model';

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './orders.component.html',
  styleUrls: ['./orders.component.scss']
})
export class OrdersComponent implements OnInit {
  private orderService = inject(OrderService);
  private authService = inject(AuthService);
  private router = inject(Router);

  orders = this.orderService.orders;
  loading = this.orderService.loading;
  error = this.orderService.error;

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
