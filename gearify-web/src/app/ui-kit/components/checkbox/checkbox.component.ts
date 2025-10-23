import { Component, Input, forwardRef, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, ReactiveFormsModule } from '@angular/forms';

/**
 * Custom Checkbox component compatible with Reactive Forms
 * Features: Custom design, checkmark animation, indeterminate state, accessibility
 */
@Component({
  selector: 'app-checkbox',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './checkbox.component.html',
  styleUrl: './checkbox.component.scss',
  providers: [{
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(() => CheckboxComponent),
    multi: true
  }]
})
export class CheckboxComponent implements ControlValueAccessor {
  @Input() label?: string;
  @Input() disabled = false;
  @Input() error?: string;
  @Input() indeterminate = false;

  checked = signal<boolean>(false);
  focused = signal<boolean>(false);

  onChange: any = () => {};
  onTouched: any = () => {};

  writeValue(value: any): void {
    this.checked.set(!!value);
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

  toggleChecked(): void {
    if (this.disabled) return;

    this.checked.update(checked => !checked);
    this.onChange(this.checked());
    this.onTouched();

    // Clear indeterminate state when toggled
    if (this.indeterminate) {
      this.indeterminate = false;
    }
  }

  onKeyDown(event: KeyboardEvent): void {
    if (this.disabled) return;

    // Toggle on Space or Enter
    if (event.key === ' ' || event.key === 'Enter') {
      event.preventDefault();
      this.toggleChecked();
    }
  }

  onFocus(): void {
    this.focused.set(true);
  }

  onBlur(): void {
    this.focused.set(false);
    this.onTouched();
  }
}
