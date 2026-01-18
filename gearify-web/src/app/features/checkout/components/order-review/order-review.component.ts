import { Component, Input, Output, EventEmitter, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ShippingAddress } from '../shipping-address/shipping-address.component';
import { PaymentDetails } from '../payment-method/payment-method.component';

@Component({
  selector: 'app-order-review',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './order-review.component.html',
  styleUrl: './order-review.component.scss'
})
export class OrderReviewComponent {
  @Input() shippingAddress: ShippingAddress | null = null;
  @Input() paymentDetails: PaymentDetails | null = null;
  @Input() total = 0;

  @Output() placeOrder = new EventEmitter<void>();
  @Output() editShipping = new EventEmitter<void>();
  @Output() editPayment = new EventEmitter<void>();

  isPlacingOrder = signal(false);

  get maskedCardNumber(): string {
    if (this.paymentDetails?.cardNumber) {
      return '**** **** **** ' + this.paymentDetails.cardNumber.slice(-4);
    }
    return '';
  }

  get paymentMethodLabel(): string {
    switch (this.paymentDetails?.type) {
      case 'card':
        return 'Credit/Debit Card';
      case 'paypal':
        return 'PayPal';
      case 'applepay':
        return 'Apple Pay';
      default:
        return '';
    }
  }

  onPlaceOrder(): void {
    this.isPlacingOrder.set(true);
    setTimeout(() => {
      this.placeOrder.emit();
      this.isPlacingOrder.set(false);
    }, 1500);
  }

  onEditShipping(): void {
    this.editShipping.emit();
  }

  onEditPayment(): void {
    this.editPayment.emit();
  }
}
