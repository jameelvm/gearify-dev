import { Component, Output, EventEmitter, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';

export type PaymentType = 'card' | 'paypal' | 'applepay';

export interface PaymentDetails {
  type: PaymentType;
  cardNumber?: string;
  cardHolder?: string;
  expiryDate?: string;
  cvv?: string;
}

@Component({
  selector: 'app-payment-method',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './payment-method.component.html',
  styleUrl: './payment-method.component.scss'
})
export class PaymentMethodComponent {
  @Output() paymentSubmitted = new EventEmitter<PaymentDetails>();
  @Output() back = new EventEmitter<void>();

  selectedPaymentType = signal<PaymentType>('card');
  isSubmitting = signal(false);

  cardForm: FormGroup;

  paymentMethods = [
    { type: 'card' as PaymentType, label: 'Credit / Debit Card', icon: 'card' },
    { type: 'paypal' as PaymentType, label: 'PayPal', icon: 'paypal' },
    { type: 'applepay' as PaymentType, label: 'Apple Pay', icon: 'apple' }
  ];

  constructor(private fb: FormBuilder) {
    this.cardForm = this.fb.group({
      cardNumber: ['', [Validators.required, Validators.pattern(/^\d{16}$/)]],
      cardHolder: ['', [Validators.required, Validators.minLength(3)]],
      expiryDate: ['', [Validators.required, Validators.pattern(/^(0[1-9]|1[0-2])\/\d{2}$/)]],
      cvv: ['', [Validators.required, Validators.pattern(/^\d{3,4}$/)]]
    });
  }

  selectPaymentType(type: PaymentType): void {
    this.selectedPaymentType.set(type);
  }

  formatCardNumber(event: Event): void {
    const input = event.target as HTMLInputElement;
    let value = input.value.replace(/\D/g, '');
    if (value.length > 16) value = value.slice(0, 16);
    this.cardForm.get('cardNumber')?.setValue(value);
  }

  formatExpiryDate(event: Event): void {
    const input = event.target as HTMLInputElement;
    let value = input.value.replace(/\D/g, '');
    if (value.length > 4) value = value.slice(0, 4);
    if (value.length >= 2) {
      value = value.slice(0, 2) + '/' + value.slice(2);
    }
    input.value = value;
    this.cardForm.get('expiryDate')?.setValue(value);
  }

  onSubmit(): void {
    if (this.selectedPaymentType() === 'card') {
      if (this.cardForm.valid) {
        this.isSubmitting.set(true);
        setTimeout(() => {
          this.paymentSubmitted.emit({
            type: 'card',
            ...this.cardForm.value
          });
          this.isSubmitting.set(false);
        }, 500);
      } else {
        this.cardForm.markAllAsTouched();
      }
    } else {
      this.isSubmitting.set(true);
      setTimeout(() => {
        this.paymentSubmitted.emit({ type: this.selectedPaymentType() });
        this.isSubmitting.set(false);
      }, 500);
    }
  }

  onBack(): void {
    this.back.emit();
  }

  getErrorMessage(field: string): string {
    const control = this.cardForm.get(field);
    if (!control?.errors || !control.touched) return '';

    if (control.errors['required']) return 'This field is required';
    if (control.errors['pattern']) {
      if (field === 'cardNumber') return 'Enter 16 digit card number';
      if (field === 'expiryDate') return 'Use MM/YY format';
      if (field === 'cvv') return 'Enter 3 or 4 digit CVV';
    }
    if (control.errors['minlength']) return `Minimum ${control.errors['minlength'].requiredLength} characters`;

    return '';
  }
}
