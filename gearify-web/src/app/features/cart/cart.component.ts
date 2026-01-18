import { Component, inject, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { CartService } from '@core/services/cart.service';
import { ButtonComponent, PriceTagComponent } from '@app/ui-kit/components';

@Component({
  selector: 'app-cart',
  standalone: true,
  imports: [CommonModule, RouterLink, ButtonComponent, PriceTagComponent],
  templateUrl: './cart.component.html',
  styleUrl: './cart.component.scss'
})
export class CartComponent implements OnInit {
  private router = inject(Router);
  cartService = inject(CartService);

  // Computed values
  cart = this.cartService.cart;
  itemCount = this.cartService.itemCount;
  subtotal = this.cartService.total;

  // Empty state
  isEmpty = computed(() => this.itemCount() === 0);

  ngOnInit(): void {
    // Reload cart from API when visiting cart page
    this.cartService.loadCart();
  }

  updateQuantity(productId: string, newQuantity: number): void {
    if (newQuantity < 1) {
      this.removeItem(productId);
      return;
    }
    this.cartService.updateCartItem(productId, { quantity: newQuantity }).subscribe();
  }

  removeItem(productId: string): void {
    this.cartService.removeFromCart(productId).subscribe();
  }

  clearCart(): void {
    this.cartService.clearCart().subscribe();
  }

  proceedToCheckout(): void {
    this.router.navigate(['/checkout']);
  }
}
