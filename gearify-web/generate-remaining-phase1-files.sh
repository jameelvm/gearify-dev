#!/bin/bash
# Complete Phase 1 File Generation Script
# Run this to generate all remaining Phase 1 files

cd "$(dirname "$0")" || exit 1

echo "Generating remaining Phase 1 files..."
echo "This will create: Services, Guards, Interceptors, App Files, Styles, and more"
echo ""

# Create directories if they don't exist
mkdir -p src/app/core/services
mkdir -p src/app/core/guards
mkdir -p src/app/core/interceptors
mkdir -p src/styles
mkdir -p .github/workflows
mkdir -p public

# 1. Cart Service
cat > src/app/core/services/cart.service.ts << 'EOF'
import { Injectable, inject, signal, computed } from '@angular/core';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { Cart, CartItem, AddToCartRequest, UpdateCartItemRequest } from '../models/cart.model';
import { API_CONFIG, STORAGE_KEYS } from '@shared/constants/api.constants';

/**
 * Shopping cart service with reactive state management
 */
@Injectable({ providedIn: 'root' })
export class CartService {
  private api = inject(ApiService);

  private cartSubject = new BehaviorSubject<Cart | null>(null);
  public cart$ = this.cartSubject.asObservable();

  // Signals for reactive UI
  private cartSignal = signal<Cart | null>(null);
  public cart = this.cartSignal.asReadonly();

  public itemCount = computed(() => {
    const cart = this.cartSignal();
    return cart?.items.reduce((sum, item) => sum + item.quantity, 0) ?? 0;
  });

  public total = computed(() => this.cartSignal()?.total ?? 0);

  constructor() {
    this.loadCart();
  }

  loadCart(): void {
    const sessionId = this.getOrCreateSessionId();
    this.api.get<Cart>(`${API_CONFIG.ENDPOINTS.CART}/${sessionId}`).pipe(
      tap(cart => {
        this.cartSubject.next(cart);
        this.cartSignal.set(cart);
      })
    ).subscribe();
  }

  addToCart(request: AddToCartRequest): Observable<Cart> {
    const sessionId = this.getOrCreateSessionId();
    return this.api.post<Cart>(`${API_CONFIG.ENDPOINTS.CART}/${sessionId}/items`, request).pipe(
      tap(cart => {
        this.cartSubject.next(cart);
        this.cartSignal.set(cart);
      })
    );
  }

  updateCartItem(itemId: string, request: UpdateCartItemRequest): Observable<Cart> {
    const sessionId = this.getOrCreateSessionId();
    return this.api.put<Cart>(`${API_CONFIG.ENDPOINTS.CART}/${sessionId}/items/${itemId}`, request).pipe(
      tap(cart => {
        this.cartSubject.next(cart);
        this.cartSignal.set(cart);
      })
    );
  }

  removeFromCart(itemId: string): Observable<Cart> {
    const sessionId = this.getOrCreateSessionId();
    return this.api.delete<Cart>(`${API_CONFIG.ENDPOINTS.CART}/${sessionId}/items/${itemId}`).pipe(
      tap(cart => {
        this.cartSubject.next(cart);
        this.cartSignal.set(cart);
      })
    );
  }

  clearCart(): Observable<void> {
    const sessionId = this.getOrCreateSessionId();
    return this.api.delete<void>(`${API_CONFIG.ENDPOINTS.CART}/${sessionId}`).pipe(
      tap(() => {
        this.cartSubject.next(null);
        this.cartSignal.set(null);
      })
    );
  }

  private getOrCreateSessionId(): string {
    let sessionId = localStorage.getItem(STORAGE_KEYS.SESSION_ID);
    if (!sessionId) {
      sessionId = this.generateSessionId();
      localStorage.setItem(STORAGE_KEYS.SESSION_ID, sessionId);
    }
    return sessionId;
  }

  private generateSessionId(): string {
    return `session_${Date.now()}_${Math.random().toString(36).substring(2, 15)}`;
  }
}
EOF

echo "[1/15] Cart Service created"

# 2. Theme Service
cat > src/app/core/services/theme.service.ts << 'EOF'
import { Injectable, signal, effect } from '@angular/core';
import { STORAGE_KEYS } from '@shared/constants/api.constants';

export type Theme = 'light' | 'dark' | 'auto';

/**
 * Theme management service
 * Handles light/dark mode switching with CSS variables
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private themeSignal = signal<Theme>(this.loadThemeFromStorage());
  public theme = this.themeSignal.asReadonly();

  constructor() {
    // Apply theme on initialization
    this.applyTheme(this.themeSignal());

    // Watch for theme changes
    effect(() => {
      const theme = this.themeSignal();
      this.applyTheme(theme);
      localStorage.setItem(STORAGE_KEYS.THEME, theme);
    });

    // Watch for system theme changes when in auto mode
    if (typeof window !== 'undefined') {
      window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
        if (this.themeSignal() === 'auto') {
          this.applyTheme('auto');
        }
      });
    }
  }

  setTheme(theme: Theme): void {
    this.themeSignal.set(theme);
  }

  toggleTheme(): void {
    const current = this.themeSignal();
    this.themeSignal.set(current === 'light' ? 'dark' : 'light');
  }

  private applyTheme(theme: Theme): void {
    if (typeof document === 'undefined') return;

    const isDark = theme === 'dark' ||
      (theme === 'auto' && window.matchMedia('(prefers-color-scheme: dark)').matches);

    document.documentElement.setAttribute('data-theme', isDark ? 'dark' : 'light');
    document.body.classList.toggle('dark-theme', isDark);
  }

  private loadThemeFromStorage(): Theme {
    if (typeof localStorage === 'undefined') return 'light';
    const stored = localStorage.getItem(STORAGE_KEYS.THEME) as Theme;
    return stored || 'light';
  }
}
EOF

echo "[2/15] Theme Service created"

# 3. Auth Guard
cat > src/app/core/guards/auth.guard.ts << 'EOF'
import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Route guard to protect authenticated routes
 */
export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  // Redirect to login with return URL
  return router.createUrlTree(['/auth/login'], {
    queryParams: { returnUrl: state.url }
  });
};
EOF

echo "[3/15] Auth Guard created"

# 4. Auth Interceptor
cat > src/app/core/interceptors/auth.interceptor.ts << 'EOF'
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

/**
 * HTTP interceptor to inject JWT token into requests
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.getAccessToken();

  if (token) {
    req = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
  }

  return next(req);
};
EOF

echo "[4/15] Auth Interceptor created"

# 5. Error Interceptor
cat > src/app/core/interceptors/error.interceptor.ts << 'EOF'
import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Global error handling interceptor
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        // Unauthorized - logout and redirect to login
        authService.logout();
        router.navigate(['/auth/login']);
      } else if (error.status === 403) {
        // Forbidden - redirect to access denied
        router.navigate(['/access-denied']);
      } else if (error.status === 404) {
        console.error('Resource not found:', error.url);
      } else if (error.status >= 500) {
        console.error('Server error:', error.message);
      }

      return throwError(() => error);
    })
  );
};
EOF

echo "[5/15] Error Interceptor created"

# 6. App Component TypeScript
cat > src/app/app.component.ts << 'EOF'
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from '@core/services/theme.service';
import { DeviceUtils } from '@shared/utils/device.utils';

/**
 * Root application component
 * Detects device type and loads appropriate shell (mobile/desktop)
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  private themeService = inject(ThemeService);

  isMobile = signal(false);
  isTablet = signal(false);
  isDesktop = signal(false);

  ngOnInit(): void {
    this.detectDevice();
    this.setupResizeListener();
  }

  private detectDevice(): void {
    this.isMobile.set(DeviceUtils.isMobile());
    this.isTablet.set(DeviceUtils.isTablet());
    this.isDesktop.set(DeviceUtils.isDesktop());
  }

  private setupResizeListener(): void {
    if (typeof window !== 'undefined') {
      window.addEventListener('resize', () => this.detectDevice());
    }
  }
}
EOF

echo "[6/15] App Component TypeScript created"

# 7. App Component HTML
cat > src/app/app.component.html << 'EOF'
<div class="app-root" [class.mobile]="isMobile()" [class.tablet]="isTablet()" [class.desktop]="isDesktop()">
  <router-outlet></router-outlet>
</div>
EOF

echo "[7/15] App Component HTML created"

# 8. App Component SCSS
cat > src/app/app.component.scss << 'EOF'
.app-root {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
  width: 100%;

  &.mobile {
    font-size: 14px;
  }

  &.tablet {
    font-size: 15px;
  }

  &.desktop {
    font-size: 16px;
  }
}
EOF

echo "[8/15] App Component SCSS created"

# 9. App Config
cat > src/app/app.config.ts << 'EOF'
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideClientHydration } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors, withFetch } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';

import { routes } from './app.routes';
import { authInterceptor } from '@core/interceptors/auth.interceptor';
import { errorInterceptor } from '@core/interceptors/error.interceptor';

/**
 * Application configuration
 * Configures routing, SSR hydration, HTTP client, and interceptors
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideClientHydration(),
    provideHttpClient(
      withFetch(),
      withInterceptors([authInterceptor, errorInterceptor])
    ),
    provideAnimations()
  ]
};
EOF

echo "[9/15] App Config created"

# 10. App Routes
cat > src/app/app.routes.ts << 'EOF'
import { Routes } from '@angular/router';
import { authGuard } from '@core/guards/auth.guard';

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
    loadChildren: () => import('./features/products/products.routes').then(m => m.PRODUCT_ROUTES)
  },
  {
    path: 'cart',
    loadComponent: () => import('./features/cart/cart.component').then(m => m.CartComponent)
  },
  {
    path: 'checkout',
    canActivate: [authGuard],
    loadComponent: () => import('./features/checkout/checkout.component').then(m => m.CheckoutComponent)
  },
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then(m => m.AUTH_ROUTES)
  },
  {
    path: 'account',
    canActivate: [authGuard],
    loadChildren: () => import('./features/account/account.routes').then(m => m.ACCOUNT_ROUTES)
  },
  {
    path: 'access-denied',
    loadComponent: () => import('./features/errors/access-denied.component').then(m => m.AccessDeniedComponent)
  },
  {
    path: '**',
    loadComponent: () => import('./features/errors/not-found.component').then(m => m.NotFoundComponent)
  }
];
EOF

echo "[10/15] App Routes created"

# 11. Main.ts
cat > src/main.ts << 'EOF'
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';

/**
 * Client-side bootstrap
 */
bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));
EOF

echo "[11/15] Main.ts created"

# 12. Main.server.ts
cat > src/main.server.ts << 'EOF'
import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';

/**
 * Server-side bootstrap for SSR
 */
const bootstrap = () => bootstrapApplication(AppComponent, appConfig);

export default bootstrap;
EOF

echo "[12/15] Main.server.ts created"

# 13. Index.html
cat > src/index.html << 'EOF'
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>Gearify - E-Commerce Platform</title>
  <base href="/">
  <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
  <meta name="description" content="Gearify - Your trusted e-commerce platform for quality products">
  <meta name="theme-color" content="#1976d2">
  <link rel="icon" type="image/x-icon" href="favicon.ico">

  <!-- Preconnect to API -->
  <link rel="preconnect" href="http://localhost:8080">

  <!-- Open Graph / Social Media -->
  <meta property="og:type" content="website">
  <meta property="og:title" content="Gearify E-Commerce">
  <meta property="og:description" content="Quality products at great prices">

  <!-- PWA Meta Tags -->
  <meta name="apple-mobile-web-app-capable" content="yes">
  <meta name="apple-mobile-web-app-status-bar-style" content="default">
</head>
<body>
  <app-root></app-root>
</body>
</html>
EOF

echo "[13/15] Index.html created"

# 14. Styles.scss
cat > src/styles.scss << 'EOF'
@import './styles/variables';
@import './styles/mixins';
@import './styles/theme';

/* Global Styles */
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

html, body {
  height: 100%;
  font-family: var(--font-family-base);
  font-size: var(--font-size-base);
  line-height: var(--line-height-base);
  color: var(--color-text-primary);
  background-color: var(--color-background);
}

body {
  overflow-x: hidden;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
}

/* Typography */
h1, h2, h3, h4, h5, h6 {
  font-weight: var(--font-weight-bold);
  line-height: var(--line-height-heading);
  margin-bottom: var(--spacing-3);
}

h1 { font-size: var(--font-size-h1); }
h2 { font-size: var(--font-size-h2); }
h3 { font-size: var(--font-size-h3); }
h4 { font-size: var(--font-size-h4); }
h5 { font-size: var(--font-size-h5); }
h6 { font-size: var(--font-size-h6); }

p {
  margin-bottom: var(--spacing-3);
}

a {
  color: var(--color-primary);
  text-decoration: none;
  transition: color var(--transition-fast);

  &:hover {
    color: var(--color-primary-dark);
  }
}

/* Buttons */
button {
  font-family: inherit;
  cursor: pointer;
  border: none;
  outline: none;
  transition: all var(--transition-fast);

  &:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }
}

/* Images */
img {
  max-width: 100%;
  height: auto;
  display: block;
}

/* Utility Classes */
.container {
  width: 100%;
  max-width: var(--container-max-width);
  margin: 0 auto;
  padding: 0 var(--spacing-4);
}

.text-center { text-align: center; }
.text-right { text-align: right; }
.text-left { text-align: left; }

.hidden { display: none !important; }
.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border-width: 0;
}
EOF

echo "[14/15] Styles.scss created"

# 15. Theme SCSS files
cat > src/styles/_variables.scss << 'EOF'
/* CSS Variables for theming */
:root {
  /* Colors - Light Theme */
  --color-primary: #1976d2;
  --color-primary-light: #42a5f5;
  --color-primary-dark: #1565c0;
  --color-secondary: #dc004e;
  --color-success: #4caf50;
  --color-warning: #ff9800;
  --color-error: #f44336;
  --color-info: #2196f3;

  /* Text Colors */
  --color-text-primary: #212121;
  --color-text-secondary: #757575;
  --color-text-disabled: #bdbdbd;
  --color-text-hint: #9e9e9e;

  /* Background Colors */
  --color-background: #ffffff;
  --color-background-paper: #f5f5f5;
  --color-background-default: #fafafa;

  /* Border Colors */
  --color-border: #e0e0e0;
  --color-divider: #bdbdbd;

  /* Shadows */
  --shadow-1: 0 1px 3px rgba(0, 0, 0, 0.12);
  --shadow-2: 0 3px 6px rgba(0, 0, 0, 0.16);
  --shadow-3: 0 10px 20px rgba(0, 0, 0, 0.19);

  /* Spacing */
  --spacing-1: 4px;
  --spacing-2: 8px;
  --spacing-3: 16px;
  --spacing-4: 24px;
  --spacing-5: 32px;
  --spacing-6: 48px;

  /* Typography */
  --font-family-base: 'Roboto', 'Helvetica', 'Arial', sans-serif;
  --font-size-base: 16px;
  --font-size-h1: 2.5rem;
  --font-size-h2: 2rem;
  --font-size-h3: 1.75rem;
  --font-size-h4: 1.5rem;
  --font-size-h5: 1.25rem;
  --font-size-h6: 1rem;
  --font-weight-light: 300;
  --font-weight-regular: 400;
  --font-weight-medium: 500;
  --font-weight-bold: 700;
  --line-height-base: 1.5;
  --line-height-heading: 1.2;

  /* Transitions */
  --transition-fast: 150ms ease-in-out;
  --transition-medium: 300ms ease-in-out;
  --transition-slow: 500ms ease-in-out;

  /* Border Radius */
  --border-radius-sm: 4px;
  --border-radius-md: 8px;
  --border-radius-lg: 16px;
  --border-radius-full: 9999px;

  /* Layout */
  --container-max-width: 1200px;
  --header-height: 64px;
  --footer-height: 200px;

  /* Z-Index */
  --z-dropdown: 1000;
  --z-sticky: 1020;
  --z-fixed: 1030;
  --z-modal-backdrop: 1040;
  --z-modal: 1050;
  --z-popover: 1060;
  --z-tooltip: 1070;
}
EOF

cat > src/styles/_mixins.scss << 'EOF'
/* Responsive Breakpoints */
@mixin mobile {
  @media (max-width: 767px) {
    @content;
  }
}

@mixin tablet {
  @media (min-width: 768px) and (max-width: 1023px) {
    @content;
  }
}

@mixin desktop {
  @media (min-width: 1024px) {
    @content;
  }
}

/* Flexbox Utilities */
@mixin flex-center {
  display: flex;
  align-items: center;
  justify-content: center;
}

@mixin flex-between {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

/* Card */
@mixin card {
  background: var(--color-background-paper);
  border-radius: var(--border-radius-md);
  box-shadow: var(--shadow-1);
  padding: var(--spacing-4);
}

/* Truncate Text */
@mixin truncate {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* Line Clamp */
@mixin line-clamp($lines) {
  display: -webkit-box;
  -webkit-line-clamp: $lines;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
EOF

cat > src/styles/_theme.scss << 'EOF'
/* Dark Theme Overrides */
[data-theme='dark'] {
  /* Colors */
  --color-primary: #90caf9;
  --color-primary-light: #e3f2fd;
  --color-primary-dark: #42a5f5;

  /* Text Colors */
  --color-text-primary: #ffffff;
  --color-text-secondary: #b0b0b0;
  --color-text-disabled: #707070;
  --color-text-hint: #808080;

  /* Background Colors */
  --color-background: #121212;
  --color-background-paper: #1e1e1e;
  --color-background-default: #181818;

  /* Border Colors */
  --color-border: #2c2c2c;
  --color-divider: #404040;

  /* Shadows */
  --shadow-1: 0 1px 3px rgba(0, 0, 0, 0.5);
  --shadow-2: 0 3px 6px rgba(0, 0, 0, 0.6);
  --shadow-3: 0 10px 20px rgba(0, 0, 0, 0.7);
}

/* Smooth theme transitions */
* {
  transition: background-color var(--transition-medium),
              color var(--transition-medium),
              border-color var(--transition-medium);
}
EOF

echo "[15/15] Theme SCSS files created"

# Create .gitignore
cat > .gitignore << 'EOF'
# See http://help.github.com/ignore-files/ for more about ignoring files.

# Compiled output
/dist
/tmp
/out-tsc
/bazel-out

# Node
/node_modules
npm-debug.log
yarn-error.log

# IDEs and editors
.idea/
.project
.classpath
.c9/
*.launch
.settings/
*.sublime-workspace

# Visual Studio Code
.vscode/*
!.vscode/settings.json
!.vscode/tasks.json
!.vscode/launch.json
!.vscode/extensions.json
.history/*

# Miscellaneous
/.angular/cache
.sass-cache/
/connect.lock
/coverage
/libpeerconnection.log
testem.log
/typings

# System files
.DS_Store
Thumbs.db

# Environment files
.env
.env.local
.env.*.local

# Testing
/playwright-report
/test-results
EOF

echo ".gitignore created"

# Create .editorconfig
cat > .editorconfig << 'EOF'
# EditorConfig helps maintain consistent coding styles
# https://editorconfig.org

root = true

[*]
charset = utf-8
indent_style = space
indent_size = 2
insert_final_newline = true
trim_trailing_whitespace = true
end_of_line = lf

[*.ts]
quote_type = single

[*.md]
max_line_length = off
trim_trailing_whitespace = false
EOF

echo ".editorconfig created"

# Create placeholder favicon
mkdir -p public
cat > public/favicon.ico.txt << 'EOF'
Placeholder for favicon.ico
Generate a proper favicon using a tool like https://favicon.io/
EOF

echo "Placeholder for favicon.ico created"

# Create CI/CD workflow
cat > .github/workflows/ci.yml << 'EOF'
name: CI/CD Pipeline

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]

jobs:
  lint:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
      - run: npm ci
      - run: npm run lint

  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
      - run: npm ci
      - run: npm test -- --coverage

  build:
    runs-on: ubuntu-latest
    needs: [lint, test]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
      - run: npm ci
      - run: npm run build
      - run: npm run build:ssr
      - uses: actions/upload-artifact@v4
        with:
          name: build-artifacts
          path: dist/

  e2e:
    runs-on: ubuntu-latest
    needs: build
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
      - run: npm ci
      - run: npx playwright install --with-deps
      - run: npm run e2e
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: playwright-report
          path: playwright-report/
          retention-days: 30
EOF

echo "CI/CD workflow created"

echo ""
echo "========================================="
echo "✅ ALL PHASE 1 FILES GENERATED!"
echo "========================================="
echo ""
echo "Created:"
echo "  - 2 Services (cart, theme)"
echo "  - 1 Guard (auth)"
echo "  - 2 Interceptors (auth, error)"
echo "  - 5 App files (component, config, routes)"
echo "  - 3 Bootstrap files (main.ts, main.server.ts, index.html)"
echo "  - 4 Style files (styles.scss + theme)"
echo "  - 1 CI/CD workflow"
echo "  - .gitignore"
echo "  - .editorconfig"
echo ""
echo "Next steps:"
echo "  1. npm install"
echo "  2. npm run build"
echo "  3. npm start"
echo ""
