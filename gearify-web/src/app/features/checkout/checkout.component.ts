import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';

@Component({
  selector: 'app-checkout',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="checkout-container">
      <h2>Checkout</h2>

      <div class="checkout-steps">
        <div class="step" [class.active]="currentStep === 1">1. Shipping</div>
        <div class="step" [class.active]="currentStep === 2">2. Payment</div>
        <div class="step" [class.active]="currentStep === 3">3. Review</div>
      </div>

      <div *ngIf="currentStep === 1" class="step-content">
        <h3>Shipping Address</h3>
        <form [formGroup]="shippingForm">
          <input formControlName="firstName" placeholder="First Name" />
          <input formControlName="lastName" placeholder="Last Name" />
          <input formControlName="street" placeholder="Street Address" />
          <input formControlName="city" placeholder="City" />
          <button (click)="nextStep()" [disabled]="!shippingForm.valid">Continue</button>
        </form>
      </div>

      <div *ngIf="currentStep === 2" class="step-content">
        <h3>Payment Method</h3>
        <button (click)="selectPayment('stripe')">Credit Card</button>
        <button (click)="selectPayment('paypal')">PayPal</button>
        <button (click)="nextStep()">Review Order</button>
      </div>

      <div *ngIf="currentStep === 3" class="step-content">
        <h3>Review Your Order</h3>
        <div class="order-summary">
          <p>Total: $299.99</p>
        </div>
        <button (click)="placeOrder()">Place Order</button>
      </div>
    </div>
  `,
  styles: [`
    .checkout-container { max-width: 800px; margin: 0 auto; padding: 2rem; }
    .checkout-steps { display: flex; justify-content: space-between; margin-bottom: 2rem; }
    .step { flex: 1; padding: 1rem; background: #f5f5f5; text-align: center; }
    .step.active { background: #1e3a8a; color: white; }
    form input { display: block; width: 100%; padding: 0.75rem; margin-bottom: 1rem; }
    button { padding: 0.75rem 1.5rem; background: #1e3a8a; color: white; border: none; cursor: pointer; }
  `]
})
export class CheckoutComponent {
  currentStep = 1;
  selectedPayment = '';
  shippingForm: FormGroup;

  constructor(private fb: FormBuilder) {
    this.shippingForm = this.fb.group({
      firstName: ['', Validators.required],
      lastName: ['', Validators.required],
      street: ['', Validators.required],
      city: ['', Validators.required]
    });
  }

  selectPayment(method: string) {
    this.selectedPayment = method;
  }

  nextStep() {
    if (this.currentStep < 3) this.currentStep++;
  }

  placeOrder() {
    alert('Order placed successfully!');
  }
}
