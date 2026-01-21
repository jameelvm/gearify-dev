import { Component, Output, EventEmitter, signal, inject, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AddressService } from '@core/services/address.service';
import { AuthService } from '@app/features/auth/auth.service';
import { Address, CreateAddressRequest } from '@core/models/address.model';

export interface ShippingAddress {
  addressId?: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  address: string;
  apartment?: string;
  city: string;
  state: string;
  zipCode: string;
  country: string;
}

type AddressMode = 'saved' | 'new';

@Component({
  selector: 'app-shipping-address',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './shipping-address.component.html',
  styleUrl: './shipping-address.component.scss'
})
export class ShippingAddressComponent implements OnInit {
  @Output() addressSubmitted = new EventEmitter<ShippingAddress>();
  @Output() back = new EventEmitter<void>();

  private addressService = inject(AddressService);
  private authService = inject(AuthService);
  private fb = inject(FormBuilder);

  shippingForm: FormGroup;
  isSubmitting = signal(false);
  addressMode = signal<AddressMode>('saved');
  selectedAddressId = signal<string | null>(null);
  saveNewAddress = signal(false);
  newAddressLabel = signal('');

  // Computed signals
  savedAddresses = this.addressService.addresses;
  isAuthenticated = this.authService.isAuthenticated;
  isLoadingAddresses = this.addressService.loading;

  hasSavedAddresses = computed(() => this.savedAddresses().length > 0);
  defaultAddress = computed(() => this.savedAddresses().find(a => a.isDefault) ?? null);

  countries = [
    { code: 'US', name: 'United States' },
    { code: 'CA', name: 'Canada' },
    { code: 'GB', name: 'United Kingdom' },
    { code: 'AU', name: 'Australia' }
  ];

  states = [
    { code: 'CA', name: 'California' },
    { code: 'NY', name: 'New York' },
    { code: 'TX', name: 'Texas' },
    { code: 'FL', name: 'Florida' },
    { code: 'WA', name: 'Washington' }
  ];

  addressLabels = ['Home', 'Work', 'Other'];

  constructor() {
    this.shippingForm = this.fb.group({
      firstName: ['', [Validators.required, Validators.minLength(2)]],
      lastName: ['', [Validators.required, Validators.minLength(2)]],
      email: ['', [Validators.required, Validators.email]],
      phone: ['', [Validators.required, Validators.pattern(/^\+?[\d\s-]{10,}$/)]],
      address: ['', [Validators.required, Validators.minLength(5)]],
      apartment: [''],
      city: ['', [Validators.required]],
      state: ['', [Validators.required]],
      zipCode: ['', [Validators.required, Validators.pattern(/^\d{5}(-\d{4})?$/)]],
      country: ['US', [Validators.required]]
    });
  }

  ngOnInit(): void {
    if (this.isAuthenticated()) {
      this.addressService.loadAddresses().subscribe({
        next: () => {
          // Auto-select default address if available, or show new address form
          const defaultAddr = this.defaultAddress();
          if (defaultAddr) {
            this.selectedAddressId.set(defaultAddr.id);
            this.addressMode.set('saved');
          } else if (!this.hasSavedAddresses()) {
            // No saved addresses - show the new address form
            this.addressMode.set('new');
          }
        }
      });

      // Pre-fill email from user profile
      const user = this.authService.user();
      if (user?.user?.email) {
        this.shippingForm.patchValue({ email: user.user.email });
      }
    } else {
      // Guest users go directly to new address form
      this.addressMode.set('new');
    }
  }

  selectSavedAddress(addressId: string): void {
    this.selectedAddressId.set(addressId);
    this.addressMode.set('saved');
  }

  switchToNewAddress(): void {
    this.addressMode.set('new');
    this.selectedAddressId.set(null);
  }

  switchToSavedAddresses(): void {
    if (this.hasSavedAddresses()) {
      this.addressMode.set('saved');
      // Select default if nothing selected
      if (!this.selectedAddressId() && this.defaultAddress()) {
        this.selectedAddressId.set(this.defaultAddress()!.id);
      }
    }
  }

  toggleSaveAddress(): void {
    this.saveNewAddress.update(v => !v);
  }

  onSubmit(): void {
    if (this.addressMode() === 'saved') {
      this.submitSavedAddress();
    } else {
      this.submitNewAddress();
    }
  }

  private submitSavedAddress(): void {
    const selectedId = this.selectedAddressId();
    if (!selectedId) return;

    const savedAddr = this.savedAddresses().find(a => a.id === selectedId);
    if (!savedAddr) return;

    this.isSubmitting.set(true);

    // Get email from form or user profile
    const user = this.authService.user();
    const email = this.shippingForm.get('email')?.value || user?.user?.email || '';

    setTimeout(() => {
      this.addressSubmitted.emit({
        addressId: savedAddr.id,
        firstName: savedAddr.firstName,
        lastName: savedAddr.lastName,
        email,
        phone: savedAddr.phone,
        address: savedAddr.addressLine1,
        apartment: savedAddr.addressLine2,
        city: savedAddr.city,
        state: savedAddr.state,
        zipCode: savedAddr.zipCode,
        country: savedAddr.country
      });
      this.isSubmitting.set(false);
    }, 500);
  }

  private submitNewAddress(): void {
    if (this.shippingForm.valid) {
      this.isSubmitting.set(true);

      // If user wants to save the address
      if (this.saveNewAddress() && this.isAuthenticated()) {
        const formValue = this.shippingForm.value;
        const createRequest: CreateAddressRequest = {
          label: this.newAddressLabel() || 'Home',
          firstName: formValue.firstName,
          lastName: formValue.lastName,
          phone: formValue.phone,
          addressLine1: formValue.address,
          addressLine2: formValue.apartment,
          city: formValue.city,
          state: formValue.state,
          zipCode: formValue.zipCode,
          country: formValue.country,
          isDefault: this.savedAddresses().length === 0 // Make default if first address
        };

        this.addressService.createAddress(createRequest).subscribe({
          next: () => {
            this.emitFormAddress();
          },
          error: () => {
            // Still emit the address even if save fails
            this.emitFormAddress();
          }
        });
      } else {
        this.emitFormAddress();
      }
    } else {
      this.shippingForm.markAllAsTouched();
    }
  }

  private emitFormAddress(): void {
    setTimeout(() => {
      this.addressSubmitted.emit(this.shippingForm.value);
      this.isSubmitting.set(false);
    }, 500);
  }

  onBack(): void {
    this.back.emit();
  }

  getErrorMessage(field: string): string {
    const control = this.shippingForm.get(field);
    if (!control?.errors || !control.touched) return '';

    if (control.errors['required']) return 'This field is required';
    if (control.errors['email']) return 'Please enter a valid email';
    if (control.errors['minlength']) return `Minimum ${control.errors['minlength'].requiredLength} characters`;
    if (control.errors['pattern']) return 'Invalid format';

    return '';
  }

  formatAddressDisplay(addr: Address): string {
    const parts = [addr.addressLine1];
    if (addr.addressLine2) parts.push(addr.addressLine2);
    parts.push(`${addr.city}, ${addr.state} ${addr.zipCode}`);
    return parts.join(', ');
  }
}
