import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type CheckoutStep = 'shipping' | 'payment' | 'review';

@Component({
  selector: 'app-checkout-steps',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './checkout-steps.component.html',
  styleUrl: './checkout-steps.component.scss'
})
export class CheckoutStepsComponent {
  @Input() currentStep: CheckoutStep = 'shipping';

  steps: { key: CheckoutStep; label: string; number: number }[] = [
    { key: 'shipping', label: 'Shipping', number: 1 },
    { key: 'payment', label: 'Payment', number: 2 },
    { key: 'review', label: 'Review', number: 3 }
  ];

  isStepCompleted(step: CheckoutStep): boolean {
    const stepOrder: CheckoutStep[] = ['shipping', 'payment', 'review'];
    return stepOrder.indexOf(step) < stepOrder.indexOf(this.currentStep);
  }

  isStepActive(step: CheckoutStep): boolean {
    return step === this.currentStep;
  }
}
