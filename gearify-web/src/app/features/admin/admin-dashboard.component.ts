import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="admin-dashboard">
      <h1>Admin Dashboard</h1>

      <div class="stats-grid">
        <div class="stat-card">
          <h3>Total Orders</h3>
          <div class="stat-value">1,247</div>
        </div>

        <div class="stat-card">
          <h3>Revenue</h3>
          <div class="stat-value">42,891 USD</div>
        </div>

        <div class="stat-card">
          <h3>Active Products</h3>
          <div class="stat-value">156</div>
        </div>

        <div class="stat-card">
          <h3>Customers</h3>
          <div class="stat-value">3,421</div>
        </div>
      </div>

      <div class="recent-orders">
        <h2>Recent Orders</h2>
        <table>
          <thead>
            <tr>
              <th>Order ID</th>
              <th>Customer</th>
              <th>Amount</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let order of recentOrders">
              <td>{{ order.id }}</td>
              <td>{{ order.customer }}</td>
              <td>{{ order.amount | currency }}</td>
              <td>{{ order.status }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  `,
  styles: [`
    .admin-dashboard { padding: 2rem; }
    .stats-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 1.5rem; margin-bottom: 2rem; }
    .stat-card { background: white; padding: 1.5rem; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
    .stat-value { font-size: 2rem; font-weight: bold; color: #1e3a8a; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 0.75rem; text-align: left; border-bottom: 1px solid #eee; }
    th { background: #f9f9f9; }
  `]
})
export class AdminDashboardComponent {
  recentOrders = [
    { id: 'ORD-001', customer: 'John Doe', amount: 299.99, status: 'confirmed' },
    { id: 'ORD-002', customer: 'Jane Smith', amount: 159.98, status: 'pending' },
    { id: 'ORD-003', customer: 'Mike Johnson', amount: 449.99, status: 'confirmed' }
  ];
}
