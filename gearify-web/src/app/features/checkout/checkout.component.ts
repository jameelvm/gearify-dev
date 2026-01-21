import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { CheckoutStepsComponent, CheckoutStep } from './components/checkout-steps/checkout-steps.component';
import { ShippingAddressComponent, ShippingAddress } from './components/shipping-address/shipping-address.component';
import { PaymentMethodComponent, PaymentDetails } from './components/payment-method/payment-method.component';
import { OrderSummaryComponent } from './components/order-summary/order-summary.component';
import { OrderReviewComponent } from './components/order-review/order-review.component';
import { CartService } from '../../core/services/cart.service';
import { OrderService } from '@core/services/order.service';
import { PaymentService } from '@core/services/payment.service';
import { AuthService } from '@app/features/auth/auth.service';
import {
  CreateOrderRequest,
  CreateOrderItemRequest,
  OrderAddressDto,
  OrderDto
} from '@core/models/order.model';
import { ProcessPaymentRequest, PaymentProviderType } from '@core/models/payment.model';

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
  private orderService = inject(OrderService);
  private paymentService = inject(PaymentService);
  private authService = inject(AuthService);
  

  currentStep = signal<CheckoutStep>('shipping');
  shippingAddress = signal<ShippingAddress | null>(null);
  paymentDetails = signal<PaymentDetails | null>(null);
  orderPlaced = signal(false);
  orderId = signal<string | null>(null);
  orderNumber = signal<string | null>(null);
  createdOrder = signal<OrderDto | null>(null);
  useSameAsBilling = signal(true); // Use shipping address as billing by default

  // Loading and error states
  isProcessing = signal(false);
  errorMessage = signal<string | null>(null);

  cart = this.cartService.cart;
  cartItems = computed(() => this.cart()?.items ?? []);

  // Calculate order totals
  subtotal = computed(() => {
    const items = this.cartItems();
    return items.reduce((sum, item) => sum + (item.product.price * item.quantity), 0);
  });

  shippingCost = computed(() => this.subtotal() > 100 ? 0 : 9.99);
  taxAmount = computed(() => this.subtotal() * 0.08);

  orderTotal = computed(() => {
    return this.subtotal() + this.shippingCost() + this.taxAmount();
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
    const address = this.shippingAddress();
    const payment = this.paymentDetails();
    const items = this.cartItems();

    if (!address || !payment || items.length === 0) {
      this.errorMessage.set('Missing required checkout information');
      return;
    }

    this.isProcessing.set(true);
    this.errorMessage.set(null);

    // Build the order request
    const orderRequest = this.buildOrderRequest(address, items);

    // Step 1: Create the order
    this.orderService.createOrder(orderRequest).subscribe({
      next: (order) => {
        this.createdOrder.set(order);
        this.orderId.set(order.id);
        this.orderNumber.set(order.orderNumber);

        // Step 2: Process payment
        this.processPayment(order, payment);
      },
      error: (err) => {
        this.isProcessing.set(false);
        this.errorMessage.set(err.error?.detail || 'Failed to create order. Please try again.');
      }
    });
  }

  private buildOrderRequest(address: ShippingAddress, items: any[]): CreateOrderRequest {
    const user = this.authService.user();
    const userId = user?.user?.id || `guest-${Date.now()}`;

    const shippingAddress: OrderAddressDto = {
      addressId: address.addressId,
      fullName: `${address.firstName} ${address.lastName}`,
      street: address.address,
      street2: address.apartment,
      city: address.city,
      state: address.state,
      postalCode: address.zipCode,
      country: address.country,
      phone: address.phone
    };

    // Use shipping address as billing address by default
    const billingAddress: OrderAddressDto = this.useSameAsBilling()
      ? { ...shippingAddress }
      : { ...shippingAddress }; // TODO: Add separate billing address support

    const orderItems: CreateOrderItemRequest[] = items.map(item => ({
      productId: item.productId,
      productSku: item.product.sku || item.productId,
      productName: item.product.name,
      productImageUrl: item.product.imageUrl,
      quantity: item.quantity,
      unitPrice: item.product.price
    }));

    return {
      userId,
      items: orderItems,
      shippingAddress,
      billingAddress,
      subtotal: this.subtotal(),
      taxAmount: this.taxAmount(),
      shippingAmount: this.shippingCost(),
      discountAmount: 0,
      currency: 'USD'
    };
  }

  private processPayment(order: OrderDto, payment: PaymentDetails): void {
    const user = this.authService.user();
    const userId = user?.user?.id || `guest-${Date.now()}`;

    // Determine payment provider and token
    const provider = this.getPaymentProvider(payment.type);
    const paymentMethodToken = this.getPaymentToken(payment);

    const paymentRequest: ProcessPaymentRequest = {
      orderId: order.id,
      userId,
      amount: order.totalAmount,
      currency: order.currency,
      provider,
      paymentMethodToken,
      idempotencyKey: this.paymentService.generateIdempotencyKey(order.id)
    };

    this.paymentService.processPayment(paymentRequest).subscribe({
      next: (paymentResult) => {
        if (paymentResult.status === 'Succeeded') {
          // Payment successful - clear cart and show success
          this.cartService.clearCart().subscribe();
          this.orderPlaced.set(true);
          this.isProcessing.set(false);
        } else if (paymentResult.status === 'Failed') {
          this.isProcessing.set(false);
          this.errorMessage.set(paymentResult.errorMessage || 'Payment failed. Please try a different payment method.');
        } else {
          // Payment pending/processing - still show success for now
          this.cartService.clearCart().subscribe();
          this.orderPlaced.set(true);
          this.isProcessing.set(false);
        }
      },
      error: (err) => {
        this.isProcessing.set(false);
        this.errorMessage.set(err.error?.detail || 'Payment processing failed. Please try again.');
      }
    });
  }

  private getPaymentProvider(paymentType: string): string {
    switch (paymentType) {
      case 'paypal':
        return PaymentProviderType.PayPal;
      case 'card':
      case 'applepay':
      default:
        return PaymentProviderType.Stripe;
    }
  }

  private getPaymentToken(payment: PaymentDetails): string {
    // For mock providers in development:
    // - Cards ending in 0000 or 4242 will succeed
    // - Cards ending in 9999 will fail
    // - PayPal tokens containing "success" will succeed

    if (payment.type === 'card' && payment.cardNumber) {
      // Use the card number as a mock token (last 4 digits determine behavior)
      return `tok_${payment.cardNumber}`;
    } else if (payment.type === 'paypal') {
      // Mock PayPal token
      return 'paypal_success_token';
    } else if (payment.type === 'applepay') {
      // Mock Apple Pay token (processed via Stripe)
      return 'tok_applepay_success';
    }

    return 'tok_default';
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

  onViewOrder(): void {
    const id = this.orderId();
    if (id) {
      this.router.navigate(['/orders', id]);
    }
  }

  clearError(): void {
    this.errorMessage.set(null);
  }

  onBillingAddressToggle(useSame: boolean): void {
    this.useSameAsBilling.set(useSame);
  }
}
