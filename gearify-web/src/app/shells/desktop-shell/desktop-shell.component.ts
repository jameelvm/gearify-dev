import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-desktop-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet],
  template: `
    <div class="desktop-shell">
      <header class="desktop-header">
        <h1>Gearify Cricket Store</h1>
        <nav>
          <a routerLink="/">Home</a>
          <a routerLink="/catalog">Catalog</a>
          <a routerLink="/cart">Cart</a>
        </nav>
      </header>
      <main class="desktop-content">
        <router-outlet></router-outlet>
      </main>
    </div>
  `,
  styles: [`
    .desktop-shell { display: flex; flex-direction: column; min-height: 100vh; }
    .desktop-header { background: #1e3a8a; color: white; padding: 1rem; }
    .desktop-content { flex: 1; padding: 2rem; }
  `]
})
export class DesktopShellComponent {}
