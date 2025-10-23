#!/bin/bash
# Phase 2: UI Kit & Shared Components Generation Script
# Creates all UI components, directives, and their tests

cd "$(dirname "$0")" || exit 1

echo "========================================="
echo "Phase 2: UI Kit & Components Generation"
echo "========================================="
echo ""
echo "This will create ~50 files:"
echo "  - 13 UI Components"
echo "  - 2 Directives"
echo "  - Component tests"
echo "  - 1 Modal service"
echo ""

# Create directory structure
mkdir -p src/app/ui-kit/components/button
mkdir -p src/app/ui-kit/components/badge
mkdir -p src/app/ui-kit/components/spinner
mkdir -p src/app/ui-kit/components/input
mkdir -p src/app/ui-kit/components/select
mkdir -p src/app/ui-kit/components/checkbox
mkdir -p src/app/ui-kit/components/product-card
mkdir -p src/app/ui-kit/components/price-tag
mkdir -p src/app/ui-kit/components/rating
mkdir -p src/app/ui-kit/components/modal
mkdir -p src/app/ui-kit/components/breadcrumb
mkdir -p src/app/ui-kit/components/pagination
mkdir -p src/app/ui-kit/components/brand-bar
mkdir -p src/app/ui-kit/components/image-gallery
mkdir -p src/app/ui-kit/directives

# ===========================================
# 1. BUTTON COMPONENT
# ===========================================

cat > src/app/ui-kit/components/button/button.component.ts << 'EOF'
import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

export type ButtonVariant = 'primary' | 'secondary' | 'outline' | 'ghost' | 'danger';
export type ButtonSize = 'small' | 'medium' | 'large';

/**
 * Reusable button component with multiple variants and sizes
 *
 * @example
 * <app-button variant="primary" size="medium" (click)="handleClick()">
 *   Click Me
 * </app-button>
 */
@Component({
  selector: 'app-button',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './button.component.html',
  styleUrl: './button.component.scss'
})
export class ButtonComponent {
  @Input() variant: ButtonVariant = 'primary';
  @Input() size: ButtonSize = 'medium';
  @Input() disabled = false;
  @Input() loading = false;
  @Input() fullWidth = false;
  @Input() type: 'button' | 'submit' | 'reset' = 'button';
  @Input() iconLeft?: string;
  @Input() iconRight?: string;

  @Output() clicked = new EventEmitter<MouseEvent>();

  handleClick(event: MouseEvent): void {
    if (!this.disabled && !this.loading) {
      this.clicked.emit(event);
    }
  }
}
EOF

cat > src/app/ui-kit/components/button/button.component.html << 'EOF'
<button
  [type]="type"
  [disabled]="disabled || loading"
  [class]="'btn btn-' + variant + ' btn-' + size"
  [class.btn-full-width]="fullWidth"
  [class.btn-loading]="loading"
  (click)="handleClick($event)"
>
  @if (loading) {
    <span class="btn-spinner"></span>
  }
  @if (iconLeft && !loading) {
    <span class="btn-icon-left">{{ iconLeft }}</span>
  }
  <span class="btn-content">
    <ng-content></ng-content>
  </span>
  @if (iconRight && !loading) {
    <span class="btn-icon-right">{{ iconRight }}</span>
  }
</button>
EOF

cat > src/app/ui-kit/components/button/button.component.scss << 'EOF'
.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--spacing-2);
  font-family: var(--font-family-base);
  font-weight: var(--font-weight-medium);
  border-radius: var(--border-radius-md);
  border: 1px solid transparent;
  cursor: pointer;
  transition: all var(--transition-fast);
  text-decoration: none;
  white-space: nowrap;

  &:focus {
    outline: 2px solid var(--color-primary);
    outline-offset: 2px;
  }

  &:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  // Sizes
  &.btn-small {
    padding: var(--spacing-1) var(--spacing-3);
    font-size: 14px;
    min-height: 32px;
  }

  &.btn-medium {
    padding: var(--spacing-2) var(--spacing-4);
    font-size: 16px;
    min-height: 40px;
  }

  &.btn-large {
    padding: var(--spacing-3) var(--spacing-5);
    font-size: 18px;
    min-height: 48px;
  }

  // Variants
  &.btn-primary {
    background-color: var(--color-primary);
    color: white;
    border-color: var(--color-primary);

    &:hover:not(:disabled) {
      background-color: var(--color-primary-dark);
      border-color: var(--color-primary-dark);
    }
  }

  &.btn-secondary {
    background-color: var(--color-secondary);
    color: white;
    border-color: var(--color-secondary);

    &:hover:not(:disabled) {
      opacity: 0.9;
    }
  }

  &.btn-outline {
    background-color: transparent;
    color: var(--color-primary);
    border-color: var(--color-primary);

    &:hover:not(:disabled) {
      background-color: var(--color-primary);
      color: white;
    }
  }

  &.btn-ghost {
    background-color: transparent;
    color: var(--color-text-primary);
    border-color: transparent;

    &:hover:not(:disabled) {
      background-color: var(--color-background-paper);
    }
  }

  &.btn-danger {
    background-color: var(--color-error);
    color: white;
    border-color: var(--color-error);

    &:hover:not(:disabled) {
      opacity: 0.9;
    }
  }

  &.btn-full-width {
    width: 100%;
  }

  &.btn-loading {
    position: relative;
    color: transparent;
    pointer-events: none;
  }
}

.btn-spinner {
  position: absolute;
  left: 50%;
  top: 50%;
  transform: translate(-50%, -50%);
  width: 16px;
  height: 16px;
  border: 2px solid currentColor;
  border-top-color: transparent;
  border-radius: 50%;
  animation: spin 0.6s linear infinite;
}

@keyframes spin {
  to { transform: translate(-50%, -50%) rotate(360deg); }
}

.btn-content {
  display: inline-flex;
  align-items: center;
}
EOF

echo "[1/50] Button component created"

# ===========================================
# 2. BADGE COMPONENT
# ===========================================

cat > src/app/ui-kit/components/badge/badge.component.ts << 'EOF'
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type BadgeVariant = 'primary' | 'success' | 'warning' | 'danger' | 'info';
export type BadgeSize = 'small' | 'medium' | 'large';

/**
 * Badge component for labels, tags, and status indicators
 */
@Component({
  selector: 'app-badge',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span [class]="'badge badge-' + variant + ' badge-' + size" [class.badge-pill]="pill">
      <ng-content></ng-content>
    </span>
  `,
  styleUrl: './badge.component.scss'
})
export class BadgeComponent {
  @Input() variant: BadgeVariant = 'primary';
  @Input() size: BadgeSize = 'medium';
  @Input() pill = false;
}
EOF

cat > src/app/ui-kit/components/badge/badge.component.scss << 'EOF'
.badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-weight: var(--font-weight-medium);
  border-radius: var(--border-radius-sm);
  white-space: nowrap;

  &.badge-small {
    padding: 2px var(--spacing-2);
    font-size: 12px;
  }

  &.badge-medium {
    padding: 4px var(--spacing-2);
    font-size: 14px;
  }

  &.badge-large {
    padding: var(--spacing-2) var(--spacing-3);
    font-size: 16px;
  }

  &.badge-pill {
    border-radius: var(--border-radius-full);
  }

  &.badge-primary {
    background-color: var(--color-primary);
    color: white;
  }

  &.badge-success {
    background-color: var(--color-success);
    color: white;
  }

  &.badge-warning {
    background-color: var(--color-warning);
    color: white;
  }

  &.badge-danger {
    background-color: var(--color-error);
    color: white;
  }

  &.badge-info {
    background-color: var(--color-info);
    color: white;
  }
}
EOF

echo "[2/50] Badge component created"

# ===========================================
# 3. SPINNER COMPONENT
# ===========================================

cat > src/app/ui-kit/components/spinner/spinner.component.ts << 'EOF'
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpinnerSize = 'small' | 'medium' | 'large';

/**
 * Loading spinner component
 */
@Component({
  selector: 'app-spinner',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './spinner.component.html',
  styleUrl: './spinner.component.scss'
})
export class SpinnerComponent {
  @Input() size: SpinnerSize = 'medium';
  @Input() overlay = false;
  @Input() message?: string;
}
EOF

cat > src/app/ui-kit/components/spinner/spinner.component.html << 'EOF'
@if (overlay) {
  <div class="spinner-overlay">
    <div class="spinner-container">
      <div [class]="'spinner spinner-' + size"></div>
      @if (message) {
        <p class="spinner-message">{{ message }}</p>
      }
    </div>
  </div>
} @else {
  <div [class]="'spinner spinner-' + size"></div>
}
EOF

cat > src/app/ui-kit/components/spinner/spinner.component.scss << 'EOF'
.spinner {
  border: 3px solid var(--color-border);
  border-top-color: var(--color-primary);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;

  &.spinner-small {
    width: 20px;
    height: 20px;
    border-width: 2px;
  }

  &.spinner-medium {
    width: 40px;
    height: 40px;
    border-width: 3px;
  }

  &.spinner-large {
    width: 60px;
    height: 60px;
    border-width: 4px;
  }
}

.spinner-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: var(--z-modal);
}

.spinner-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--spacing-3);
}

.spinner-message {
  color: white;
  font-size: 16px;
  margin: 0;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
EOF

echo "[3/50] Spinner component created"

# ===========================================
# 4. INPUT COMPONENT
# ===========================================

cat > src/app/ui-kit/components/input/input.component.ts << 'EOF'
import { Component, Input, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, ReactiveFormsModule } from '@angular/forms';

/**
 * Form input component compatible with Reactive Forms
 */
@Component({
  selector: 'app-input',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './input.component.html',
  styleUrl: './input.component.scss',
  providers: [{
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(() => InputComponent),
    multi: true
  }]
})
export class InputComponent implements ControlValueAccessor {
  @Input() label?: string;
  @Input() placeholder = '';
  @Input() type = 'text';
  @Input() error?: string;
  @Input() disabled = false;
  @Input() icon?: string;

  value = '';
  onChange: any = () => {};
  onTouched: any = () => {};

  writeValue(value: any): void {
    this.value = value || '';
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
  }

  onInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.value = value;
    this.onChange(value);
    this.onTouched();
  }
}
EOF

cat > src/app/ui-kit/components/input/input.component.html << 'EOF'
<div class="input-wrapper">
  @if (label) {
    <label class="input-label">{{ label }}</label>
  }
  <div class="input-container" [class.input-error]="error">
    @if (icon) {
      <span class="input-icon">{{ icon }}</span>
    }
    <input
      [type]="type"
      [placeholder]="placeholder"
      [disabled]="disabled"
      [value]="value"
      (input)="onInput($event)"
      (blur)="onTouched()"
      class="input-field"
      [class.input-with-icon]="icon"
    />
  </div>
  @if (error) {
    <span class="input-error-message">{{ error }}</span>
  }
</div>
EOF

cat > src/app/ui-kit/components/input/input.component.scss << 'EOF'
.input-wrapper {
  display: flex;
  flex-direction: column;
  gap: var(--spacing-1);
}

.input-label {
  font-size: 14px;
  font-weight: var(--font-weight-medium);
  color: var(--color-text-primary);
}

.input-container {
  position: relative;
  display: flex;
  align-items: center;
}

.input-icon {
  position: absolute;
  left: var(--spacing-3);
  color: var(--color-text-secondary);
  pointer-events: none;
}

.input-field {
  width: 100%;
  padding: var(--spacing-2) var(--spacing-3);
  font-size: 16px;
  font-family: var(--font-family-base);
  color: var(--color-text-primary);
  background-color: var(--color-background);
  border: 1px solid var(--color-border);
  border-radius: var(--border-radius-md);
  transition: all var(--transition-fast);

  &.input-with-icon {
    padding-left: calc(var(--spacing-3) * 2 + 16px);
  }

  &:focus {
    outline: none;
    border-color: var(--color-primary);
    box-shadow: 0 0 0 3px rgba(25, 118, 210, 0.1);
  }

  &:disabled {
    opacity: 0.6;
    cursor: not-allowed;
    background-color: var(--color-background-paper);
  }

  &::placeholder {
    color: var(--color-text-hint);
  }
}

.input-container.input-error .input-field {
  border-color: var(--color-error);

  &:focus {
    box-shadow: 0 0 0 3px rgba(244, 67, 54, 0.1);
  }
}

.input-error-message {
  font-size: 12px;
  color: var(--color-error);
}
EOF

echo "[4/50] Input component created"

# ===========================================
# 5. PRICE TAG COMPONENT
# ===========================================

cat > src/app/ui-kit/components/price-tag/price-tag.component.ts << 'EOF'
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { formatCurrency } from '@shared/utils/currency.utils';

/**
 * Price display component with discount support
 */
@Component({
  selector: 'app-price-tag',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './price-tag.component.html',
  styleUrl: './price-tag.component.scss'
})
export class PriceTagComponent {
  @Input() price!: number;
  @Input() compareAtPrice?: number;
  @Input() currency = 'USD';
  @Input() size: 'small' | 'medium' | 'large' = 'medium';

  get formattedPrice(): string {
    return formatCurrency(this.price, this.currency);
  }

  get formattedComparePrice(): string | null {
    return this.compareAtPrice ? formatCurrency(this.compareAtPrice, this.currency) : null;
  }

  get discountPercentage(): number | null {
    if (!this.compareAtPrice || this.compareAtPrice <= this.price) {
      return null;
    }
    return Math.round(((this.compareAtPrice - this.price) / this.compareAtPrice) * 100);
  }
}
EOF

cat > src/app/ui-kit/components/price-tag/price-tag.component.html << 'EOF'
<div [class]="'price-tag price-tag-' + size">
  <span class="price-current">{{ formattedPrice }}</span>
  @if (formattedComparePrice) {
    <span class="price-compare">{{ formattedComparePrice }}</span>
  }
  @if (discountPercentage) {
    <span class="price-discount">-{{ discountPercentage }}%</span>
  }
</div>
EOF

cat > src/app/ui-kit/components/price-tag/price-tag.component.scss << 'EOF'
.price-tag {
  display: inline-flex;
  align-items: center;
  gap: var(--spacing-2);
  flex-wrap: wrap;
}

.price-current {
  font-weight: var(--font-weight-bold);
  color: var(--color-primary);
}

.price-compare {
  text-decoration: line-through;
  color: var(--color-text-secondary);
  font-size: 0.9em;
}

.price-discount {
  background-color: var(--color-error);
  color: white;
  padding: 2px var(--spacing-2);
  border-radius: var(--border-radius-sm);
  font-size: 0.75em;
  font-weight: var(--font-weight-bold);
}

.price-tag-small {
  font-size: 14px;
}

.price-tag-medium {
  font-size: 18px;
}

.price-tag-large {
  font-size: 24px;
}
EOF

echo "[5/50] Price Tag component created"

# Continue script in next section due to length...
echo ""
echo "Creating remaining components..."
echo ""
EOF
