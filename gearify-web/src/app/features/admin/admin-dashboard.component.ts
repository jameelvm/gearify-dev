import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['./admin-dashboard.component.scss']
})
export class AdminDashboardComponent {
  recentOrders = [
    { id: 'ORD-001', customer: 'John Doe', amount: 299.99, status: 'confirmed' },
    { id: 'ORD-002', customer: 'Jane Smith', amount: 159.98, status: 'pending' },
    { id: 'ORD-003', customer: 'Mike Johnson', amount: 449.99, status: 'confirmed' }
  ];
}
