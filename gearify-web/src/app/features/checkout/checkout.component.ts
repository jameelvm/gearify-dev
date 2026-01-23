import { Component, OnInit, OnDestroy, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subject, takeUntil, interval, switchMap, filter, take, tap } from 'rxjs';
import { CheckoutStepsComponent, CheckoutStep } from './components/checkout-steps/checkout-steps.component';
import { ShippingAddressComponent, ShippingAddress } from './components/shipping-address/shipping-address.component';
import { PaymentMethodComponent, PaymentDetails } from './components/payment-method/payment-method.component';
import { OrderSummaryComponent } from './components/order-summary/order-summary.component';
import { OrderReviewComponent } from './components/order-review/order-review.component';
import { CartService } from '../../core/services/cart.service';
import { OrderService } from '@core/services/order.service';
import { AuthService } from '@app/features/auth/auth.service';
import {
  CreateOrderRequest,
  CreateOrderItemRequest,
  OrderAddressDto,
  OrderDto
} from '@core/models/order.model';

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
export class CheckoutComponent implements OnInit, OnDestroy {
  private router = inject(Router);
  private cartService = inject(CartService);
  private orderService = inject(OrderService);
  private authService = inject(AuthService);
  private destroy$ = new Subject<void>();

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
  processingStatus = signal<string>('Creating order...');
  errorMessage = signal<string | null>(null);

  // Polling config
  private readonly POLL_INTERVAL = 2000; // 2 seconds
  private readonly MAX_POLL_ATTEMPTS = 30; // 1 minute max wait

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

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
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
    this.processingStatus.set('Creating order...');
    this.errorMessage.set(null);

    // Build the order request
    const orderRequest = this.buildOrderRequest(address, items);

    // Create the order - payment will be processed automatically via event-driven saga
    this.orderService.createOrder(orderRequest).subscribe({
      next: (order) => {
        this.createdOrder.set(order);
        this.orderId.set(order.id);
        this.orderNumber.set(order.orderNumber);

        this.processingStatus.set('Processing payment...');

        // Poll for order status until payment is complete
        this.pollOrderStatus(order.id);
      },
      error: (err) => {
        this.isProcessing.set(false);
        this.errorMessage.set(err.error?.detail || 'Failed to create order. Please try again.');
      }
    });
  }

  private pollOrderStatus(orderId: string): void {
    let attempts = 0;

    interval(this.POLL_INTERVAL).pipe(
      takeUntil(this.destroy$),
      take(this.MAX_POLL_ATTEMPTS),
      tap(() => {
        attempts++;
        if (attempts > 5) {
          this.processingStatus.set('Verifying payment...');
        }
      }),
      switchMap(() => this.orderService.getOrder(orderId)),
      filter(order => {
        // Update the order in state
        this.createdOrder.set(order);

        // Check if order has reached a terminal state
        const terminalStatuses = ['Paid', 'PaymentFailed', 'Cancelled'];
        return terminalStatuses.includes(order.status);
      }),
      take(1)
    ).subscribe({
      next: (order) => {
        this.handleOrderStatusUpdate(order);
      },
      error: (err) => {
        console.error('Error polling order status:', err);
        // On error, still consider order created successfully
        // User can check order details later
        this.cartService.clearCart().subscribe();
        this.orderPlaced.set(true);
        this.isProcessing.set(false);
      },
      complete: () => {
        // If polling completes without finding terminal status, show success anyway
        if (this.isProcessing()) {
          this.cartService.clearCart().subscribe();
          this.orderPlaced.set(true);
          this.isProcessing.set(false);
        }
      }
    });
  }

  private handleOrderStatusUpdate(order: OrderDto): void {
    this.createdOrder.set(order);

    if (order.status === 'Paid') {
      // Payment successful
      this.cartService.clearCart().subscribe();
      this.orderPlaced.set(true);
      this.isProcessing.set(false);
    } else if (order.status === 'PaymentFailed' || order.status === 'Cancelled') {
      // Payment failed
      this.isProcessing.set(false);
      this.errorMessage.set('Payment failed. Please try again or contact support.');
    }
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
