import { Component } from '@angular/core';
import { RouterOutlet, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink],
  template: `
    <div class="app-shell">
      <header class="app-header">
        <h1>Gearify Cricket Store</h1>
        <nav>
          <a routerLink="/" routerLinkActive="active">Home</a>
          <a routerLink="/catalog" routerLinkActive="active">Catalog</a>
        </nav>
      </header>
      <main class="app-content">
        <router-outlet></router-outlet>
      </main>
      <footer class="app-footer">
        <p>&copy; 2025 Gearify - Your Cricket Gear Store</p>
      </footer>
    </div>
  `,
  styles: [`
    .app-shell {
      display: flex;
      flex-direction: column;
      min-height: 100vh;
    }
    .app-header {
      background: #1e3a8a;
      color: white;
      padding: 1.5rem 2rem;
    }
    .app-header h1 {
      margin: 0 0 1rem 0;
      font-size: 2rem;
    }
    .app-header nav {
      display: flex;
      gap: 1.5rem;
    }
    .app-header nav a {
      color: white;
      text-decoration: none;
      padding: 0.5rem 1rem;
      border-radius: 4px;
      transition: background 0.3s;
    }
    .app-header nav a:hover {
      background: rgba(255, 255, 255, 0.1);
    }
    .app-header nav a.active {
      background: rgba(255, 255, 255, 0.2);
    }
    .app-content {
      flex: 1;
      padding: 2rem;
      max-width: 1200px;
      margin: 0 auto;
      width: 100%;
    }
    .app-footer {
      background: #f3f4f6;
      padding: 1.5rem;
      text-align: center;
      color: #6b7280;
    }
  `]
})
export class AppComponent {
  title = 'gearify-web';
}
