import { Component, Input, Output, EventEmitter } from '@angular/core';
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
  @Input() isProcessing = false;
  @Input() processingStatus = 'Processing...';
  @Input() errorMessage: string | null = null;
  @Input() useSameAsBilling = true;

  @Output() placeOrder = new EventEmitter<void>();
  @Output() editShipping = new EventEmitter<void>();
  @Output() editPayment = new EventEmitter<void>();
  @Output() clearError = new EventEmitter<void>();
  @Output() billingAddressToggle = new EventEmitter<boolean>();

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
    if (!this.isProcessing) {
      this.placeOrder.emit();
    }
  }

  onEditShipping(): void {
    if (!this.isProcessing) {
      this.editShipping.emit();
    }
  }

  onEditPayment(): void {
    if (!this.isProcessing) {
      this.editPayment.emit();
    }
  }

  onClearError(): void {
    this.clearError.emit();
  }

  onBillingToggle(event: Event): void {
    const checkbox = event.target as HTMLInputElement;
    this.billingAddressToggle.emit(checkbox.checked);
  }
}
