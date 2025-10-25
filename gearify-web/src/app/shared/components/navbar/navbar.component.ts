import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, Router } from '@angular/router';
import { AuthService } from '@core/services/auth.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <nav class="navbar">
      <div class="navbar-container">
        <div class="navbar-brand">
          <a routerLink="/" class="brand-logo">
            <span class="brand-name">Gearify</span>
          </a>
        </div>

        <div class="navbar-menu">
          <a routerLink="/home" class="nav-link">Home</a>
          <a routerLink="/products" class="nav-link">Products</a>
          <a routerLink="/showcase" class="nav-link">Showcase</a>
        </div>

        <div class="navbar-actions">
          @if (authService.isAuthenticated()) {
            <div class="user-menu">
              <button class="user-button" (click)="toggleUserMenu()">
                <div class="user-avatar">
                  {{ getUserInitials() }}
                </div>
                <span class="user-name">{{ getUserName() }}</span>
                <svg class="chevron" width="20" height="20" viewBox="0 0 20 20" fill="currentColor">
                  <path fill-rule="evenodd" d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" clip-rule="evenodd" />
                </svg>
              </button>

              @if (showUserMenu) {
                <div class="user-dropdown">
                  <div class="user-info">
                    <p class="user-email">{{ getUser()?.email }}</p>
                    <span class="user-role">{{ getUser()?.role }}</span>
                  </div>
                  <div class="dropdown-divider"></div>
                  <a routerLink="/account/profile" class="dropdown-item" (click)="closeUserMenu()">
                    <svg width="20" height="20" fill="currentColor">
                      <path d="M10 9a3 3 0 100-6 3 3 0 000 6zm-7 9a7 7 0 1114 0H3z"/>
                    </svg>
                    Profile
                  </a>
                  <a routerLink="/account/orders" class="dropdown-item" (click)="closeUserMenu()">
                    <svg width="20" height="20" fill="currentColor">
                      <path d="M3 1a1 1 0 000 2h1.22l.305 1.222a.997.997 0 00.01.042l1.358 5.43-.893.892C3.74 11.846 4.632 14 6.414 14H15a1 1 0 000-2H6.414l1-1H14a1 1 0 00.894-.553l3-6A1 1 0 0017 3H6.28l-.31-1.243A1 1 0 005 1H3zM16 16.5a1.5 1.5 0 11-3 0 1.5 1.5 0 013 0zM6.5 18a1.5 1.5 0 100-3 1.5 1.5 0 000 3z"/>
                    </svg>
                    Orders
                  </a>
                  <button class="dropdown-item" (click)="logout()">
                    <svg width="20" height="20" fill="currentColor">
                      <path fill-rule="evenodd" d="M3 3a1 1 0 00-1 1v12a1 1 0 102 0V4a1 1 0 00-1-1zm10.293 9.293a1 1 0 001.414 1.414l3-3a1 1 0 000-1.414l-3-3a1 1 0 10-1.414 1.414L14.586 9H7a1 1 0 100 2h7.586l-1.293 1.293z" clip-rule="evenodd"/>
                    </svg>
                    Logout
                  </button>
                </div>
              }
            </div>
          } @else {
            <div class="auth-buttons">
              <a routerLink="/auth/login" class="btn-secondary">Login</a>
              <a routerLink="/auth/register" class="btn-primary">Sign Up</a>
            </div>
          }
        </div>
      </div>
    </nav>
  `,
  styles: [`
    .navbar {
      background: white;
      border-bottom: 1px solid #e5e7eb;
      padding: 1rem 0;
      position: sticky;
      top: 0;
      z-index: 1000;
      box-shadow: 0 1px 3px 0 rgba(0, 0, 0, 0.1);
    }

    .navbar-container {
      max-width: 1280px;
      margin: 0 auto;
      padding: 0 1.5rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 2rem;
    }

    .navbar-brand .brand-logo {
      text-decoration: none;
      display: flex;
      align-items: center;
    }

    .brand-name {
      font-size: 1.5rem;
      font-weight: 700;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
    }

    .navbar-menu {
      display: flex;
      gap: 2rem;
      flex: 1;
    }

    .nav-link {
      text-decoration: none;
      color: #4b5563;
      font-weight: 500;
      font-size: 0.9375rem;
      transition: color 0.2s;
    }

    .nav-link:hover {
      color: #667eea;
    }

    .navbar-actions {
      display: flex;
      align-items: center;
      gap: 1rem;
    }

    .auth-buttons {
      display: flex;
      gap: 0.75rem;
    }

    .btn-primary, .btn-secondary {
      padding: 0.5rem 1.25rem;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.875rem;
      text-decoration: none;
      transition: all 0.2s;
      border: none;
      cursor: pointer;
    }

    .btn-primary {
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
    }

    .btn-primary:hover {
      transform: translateY(-1px);
      box-shadow: 0 4px 12px rgba(102, 126, 234, 0.3);
    }

    .btn-secondary {
      background: transparent;
      color: #667eea;
      border: 1px solid #667eea;
    }

    .btn-secondary:hover {
      background: #f3f4f6;
    }

    .user-menu {
      position: relative;
    }

    .user-button {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.5rem 1rem;
      background: white;
      border: 1px solid #e5e7eb;
      border-radius: 8px;
      cursor: pointer;
      transition: all 0.2s;
    }

    .user-button:hover {
      background: #f9fafb;
      border-color: #d1d5db;
    }

    .user-avatar {
      width: 32px;
      height: 32px;
      border-radius: 50%;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 600;
      font-size: 0.875rem;
    }

    .user-name {
      font-weight: 500;
      color: #1f2937;
      font-size: 0.9375rem;
    }

    .chevron {
      color: #9ca3af;
      transition: transform 0.2s;
    }

    .user-button:hover .chevron {
      color: #6b7280;
    }

    .user-dropdown {
      position: absolute;
      top: calc(100% + 0.5rem);
      right: 0;
      min-width: 240px;
      background: white;
      border: 1px solid #e5e7eb;
      border-radius: 8px;
      box-shadow: 0 10px 25px rgba(0, 0, 0, 0.1);
      padding: 0.5rem 0;
      z-index: 50;
    }

    .user-info {
      padding: 0.75rem 1rem;
    }

    .user-email {
      font-size: 0.875rem;
      font-weight: 500;
      color: #1f2937;
      margin-bottom: 0.25rem;
    }

    .user-role {
      font-size: 0.75rem;
      color: #6b7280;
      padding: 0.125rem 0.5rem;
      background: #f3f4f6;
      border-radius: 4px;
    }

    .dropdown-divider {
      height: 1px;
      background: #e5e7eb;
      margin: 0.5rem 0;
    }

    .dropdown-item {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.75rem 1rem;
      color: #4b5563;
      font-size: 0.875rem;
      text-decoration: none;
      transition: background 0.2s;
      cursor: pointer;
      border: none;
      background: none;
      width: 100%;
      text-align: left;
    }

    .dropdown-item:hover {
      background: #f9fafb;
      color: #1f2937;
    }

    .dropdown-item svg {
      flex-shrink: 0;
    }

    @media (max-width: 768px) {
      .navbar-menu {
        display: none;
      }

      .user-name {
        display: none;
      }
    }
  `]
})
export class NavbarComponent {
  authService = inject(AuthService);
  private router = inject(Router);

  showUserMenu = false;

  getUser() {
    return this.authService.user()?.user;
  }

  getUserName(): string {
    const user = this.getUser();
    return user ? `${user.firstName} ${user.lastName}` : '';
  }

  getUserInitials(): string {
    const user = this.getUser();
    if (!user) return '';
    return `${user.firstName?.charAt(0) || ''}${user.lastName?.charAt(0) || ''}`.toUpperCase();
  }

  toggleUserMenu(): void {
    this.showUserMenu = !this.showUserMenu;
  }

  closeUserMenu(): void {
    this.showUserMenu = false;
  }

  logout(): void {
    this.authService.logout();
    this.showUserMenu = false;
  }
}
