import { Component, inject, signal, effect, untracked, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CartService } from '@core/services/cart.service';

@Component({
  selector: 'app-cart-icon',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './cart-icon.component.html',
  styleUrl: './cart-icon.component.scss'
})
export class CartIconComponent implements OnInit {
  cartService = inject(CartService);

  isAnimating = signal(false);
  private previousCount: number | null = null;

  ngOnInit(): void {
    this.cartService.loadCart();
  }

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
