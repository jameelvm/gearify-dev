import { Routes } from '@angular/router';
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
    loadComponent: () => import('./features/home/home.component').then(m => m.HomeComponent)
  },
  {
    path: 'products',
    loadComponent: () => import('./features/products/products-list.component').then(m => m.ProductsListComponent)
  },
  {
    path: 'products/:id',
    loadComponent: () => import('./features/products/product-detail.component').then(m => m.ProductDetailComponent)
  },
  {
    path: 'showcase',
    loadComponent: () => import('./features/ui-showcase/ui-showcase.component').then(m => m.UiShowcaseComponent)
  },
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then(m => m.AUTH_ROUTES)
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
  //   path: 'account',
  //   canActivate: [authGuard],
  //   loadChildren: () => import('./features/account/account.routes').then(m => m.ACCOUNT_ROUTES)
  // },
  // {
  //   path: '**',
  //   loadComponent: () => import('./features/errors/not-found.component').then(m => m.NotFoundComponent)
  // }
];
