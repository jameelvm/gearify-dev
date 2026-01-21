import { Routes } from '@angular/router';

export const ACCOUNT_ROUTES: Routes = [
  {
    path: 'profile',
    loadComponent: () => import('./profile/profile.component').then(m => m.ProfileComponent)
  },
  {
    path: 'orders',
    loadComponent: () => import('./orders/orders.component').then(m => m.OrdersComponent)
  },
  {
    path: 'orders/:id',
    loadComponent: () => import('./orders/order-detail/order-detail.component').then(m => m.OrderDetailComponent)
  },
  {
    path: '',
    redirectTo: 'profile',
    pathMatch: 'full'
  }
];
