import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-mobile-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet],
  template: `
    <div class="mobile-shell">
      <header class="mobile-header">
        <h1>Gearify</h1>
      </header>
      <main class="mobile-content">
        <router-outlet></router-outlet>
      </main>
      <nav class="mobile-nav">
        <a routerLink="/">Home</a>
        <a routerLink="/catalog">Shop</a>
        <a routerLink="/cart">Cart</a>
      </nav>
    </div>
  `,
  styles: [`
    .mobile-shell { display: flex; flex-direction: column; height: 100vh; }
    .mobile-header { padding: 1rem; background: #1e3a8a; color: white; }
    .mobile-content { flex: 1; overflow-y: auto; }
    .mobile-nav { display: flex; justify-content: space-around; padding: 0.5rem; background: #f5f5f5; }
  `]
})
export class MobileShellComponent {}
