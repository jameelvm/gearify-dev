import { Routes } from '@angular/router';
import { tenantGuard } from './guards/tenant.guard';
// import { authGuard } from '@core/guards/auth.guard';

/**
 * Application routes with lazy loading
 */
export const routes: Routes = [
  {
    path: '',
    redirectTo: '/home',
    pathMatch: 'full'
  },
  {
    path: 'home',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/home/home.component').then(m => m.HomeComponent)
  },
  {
    path: 'products',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/products/products-list.component').then(m => m.ProductsListComponent)
  },
  {
    path: 'search',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/products/products-list.component').then(m => m.ProductsListComponent)
  },
  {
    path: 'products/:id',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/products/product-detail.component').then(m => m.ProductDetailComponent)
  },
  {
    path: 'showcase',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/ui-showcase/ui-showcase.component').then(m => m.UiShowcaseComponent)
  },
  {
    path: 'auth',
    canActivate: [tenantGuard],
    loadChildren: () => import('./features/auth/auth.routes').then(m => m.AUTH_ROUTES)
  },
  {
    path: 'account',
    canActivate: [tenantGuard],
    loadChildren: () => import('./features/account/account.routes').then(m => m.ACCOUNT_ROUTES)
  },
  {
    path: 'cart',
    canActivate: [tenantGuard],
    loadChildren: () => import('./features/cart/cart.routes').then(m => m.CART_ROUTES)
  },
  {
    path: 'checkout',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/checkout/checkout.component').then(m => m.CheckoutComponent)
  },
  {
    path: 'tenant-not-found',
    loadComponent: () => import('./features/errors/tenant-not-found.component').then(m => m.TenantNotFoundComponent)
  },
  // Catalog navigation routes (slug-based, clean URLs)
  {
    path: ':departmentSlug/:categorySlug/price/:range',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/products/products-list.component').then(m => m.ProductsListComponent)
  },
  {
    path: ':departmentSlug/:categorySlug/brand/:brandSlug',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/products/products-list.component').then(m => m.ProductsListComponent)
  },
  {
    path: ':departmentSlug/:categorySlug/:subcategorySlug',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/products/products-list.component').then(m => m.ProductsListComponent)
  },
  {
    path: ':departmentSlug/:categorySlug',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/products/products-list.component').then(m => m.ProductsListComponent)
  },
  {
    path: ':departmentSlug',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/products/products-list.component').then(m => m.ProductsListComponent)
  }
  // Future routes to be implemented:
  // {
  //   path: 'cart',
  //   loadComponent: () => import('./features/cart/cart.component').then(m => m.CartComponent)
  // },
  // {
  //   path: 'checkout',
  //   canActivate: [authGuard],
  //   loadComponent: () => import('./features/checkout/checkout.component').then(m => m.CheckoutComponent)
  // },
  // {
  //   path: '**',
  //   loadComponent: () => import('./features/errors/not-found.component').then(m => m.NotFoundComponent)
  // }
];
