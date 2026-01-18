import { Component, inject, signal, effect, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CartService } from '@core/services/cart.service';

@Component({
  selector: 'app-cart-icon',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <a routerLink="/cart" class="cart-icon-link" aria-label="Shopping cart">
      <div class="cart-icon-container">
        <svg
          class="cart-icon"
          width="24"
          height="24"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <circle cx="9" cy="21" r="1"></circle>
          <circle cx="20" cy="21" r="1"></circle>
          <path d="m1 1 4 4 2.68 12.72a2 2 0 0 0 2 1.58h9.72a2 2 0 0 0 2-1.6L23 6H6"></path>
        </svg>
        @if (cartService.itemCount() > 0) {
          <span class="cart-badge" [class.animate]="isAnimating()">{{ cartService.itemCount() }}</span>
        }
      </div>
    </a>
  `,
  styles: [`
    .cart-icon-link {
      display: flex;
      align-items: center;
      justify-content: center;
      text-decoration: none;
      color: #4b5563;
      transition: color 0.2s ease;

      &:hover {
        color: #6200EA;
      }
    }

    .cart-icon-container {
      position: relative;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 8px;
    }

    .cart-icon {
      width: 24px;
      height: 24px;
    }

    .cart-badge {
      position: absolute;
      top: -2px;
      right: -4px;
      min-width: 18px;
      height: 18px;
      padding: 0 5px;
      font-size: 11px;
      font-weight: 600;
      line-height: 18px;
      text-align: center;
      color: white;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      border-radius: 9px;
      box-shadow: 0 2px 4px rgba(102, 126, 234, 0.4);
    }

    .cart-badge.animate {
      animation: bounce-pop 0.4s ease-out forwards;
    }

    @keyframes bounce-pop {
      0% {
        transform: scale(1);
      }
      25% {
        transform: scale(1.5);
      }
      50% {
        transform: scale(0.85);
      }
      75% {
        transform: scale(1.2);
      }
      100% {
        transform: scale(1);
      }
    }
  `]
})
export class CartIconComponent {
  cartService = inject(CartService);

  isAnimating = signal(false);
  private previousCount: number | null = null;

  constructor() {
    effect(() => {
      const currentCount = this.cartService.itemCount();

      untracked(() => {
        // Skip initial load, only animate on subsequent increases
        if (this.previousCount !== null && currentCount > this.previousCount) {
          this.isAnimating.set(false);
          // Force reflow to restart animation
          setTimeout(() => this.isAnimating.set(true), 10);
          setTimeout(() => this.isAnimating.set(false), 450);
        }
        this.previousCount = currentCount;
      });
    });
  }
}
