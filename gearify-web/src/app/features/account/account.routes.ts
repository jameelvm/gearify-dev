import { Routes } from '@angular/router';

export const ACCOUNT_ROUTES: Routes = [
  {
    path: 'profile',
    loadComponent: () => import('./profile/profile.component').then(m => m.ProfileComponent)
  },
  {
    path: 'orders',
    loadComponent: () => import('./profile/profile.component').then(m => m.ProfileComponent) // Placeholder - will be replaced later
  },
  {
    path: '',
    redirectTo: 'profile',
    pathMatch: 'full'
  }
];
