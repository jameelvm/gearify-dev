import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { CheckoutStepsComponent, CheckoutStep } from './components/checkout-steps/checkout-steps.component';
import { ShippingAddressComponent, ShippingAddress } from './components/shipping-address/shipping-address.component';
import { PaymentMethodComponent, PaymentDetails } from './components/payment-method/payment-method.component';
import { OrderSummaryComponent } from './components/order-summary/order-summary.component';
import { OrderReviewComponent } from './components/order-review/order-review.component';
import { CartService } from '../../core/services/cart.service';

@Component({
  selector: 'app-checkout',
  standalone: true,
  imports: [
    CommonModule,
    CheckoutStepsComponent,
    ShippingAddressComponent,
    PaymentMethodComponent,
    OrderSummaryComponent,
    OrderReviewComponent
  ],
  templateUrl: './checkout.component.html',
  styleUrl: './checkout.component.scss'
})
export class CheckoutComponent implements OnInit {
  private router = inject(Router);
  private cartService = inject(CartService);

  currentStep = signal<CheckoutStep>('shipping');
  shippingAddress = signal<ShippingAddress | null>(null);
  paymentDetails = signal<PaymentDetails | null>(null);
  orderPlaced = signal(false);
  orderId = signal<string | null>(null);

  cart = this.cartService.cart;
  cartItems = computed(() => this.cart()?.items ?? []);

  orderTotal = computed(() => {
    const items = this.cartItems();
    const subtotal = items.reduce((sum, item) => sum + (item.product.price * item.quantity), 0);
    const shipping = subtotal > 100 ? 0 : 9.99;
    const tax = subtotal * 0.08;
    return subtotal + shipping + tax;
  });

  ngOnInit(): void {
    if (!this.cart() || this.cartItems().length === 0) {
      this.router.navigate(['/cart']);
    }
  }

  onShippingSubmitted(address: ShippingAddress): void {
    this.shippingAddress.set(address);
    this.currentStep.set('payment');
  }

  onPaymentSubmitted(payment: PaymentDetails): void {
    this.paymentDetails.set(payment);
    this.currentStep.set('review');
  }

  onPlaceOrder(): void {
    const newOrderId = 'ORD-' + Date.now().toString(36).toUpperCase();
    this.orderId.set(newOrderId);
    this.orderPlaced.set(true);
  }

  onBackToCart(): void {
    this.router.navigate(['/cart']);
  }

  onBackToShipping(): void {
    this.currentStep.set('shipping');
  }

  onBackToPayment(): void {
    this.currentStep.set('payment');
  }

  onEditShipping(): void {
    this.currentStep.set('shipping');
  }

  onEditPayment(): void {
    this.currentStep.set('payment');
  }

  onContinueShopping(): void {
    this.router.navigate(['/products']);
  }
}
