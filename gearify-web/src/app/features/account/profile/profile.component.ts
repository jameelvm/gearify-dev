import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '@features/auth/auth.service';
import { Router } from '@angular/router';
import { UserService } from '@core/services/user.service';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.scss']
})
export class ProfileComponent implements OnInit {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private userService = inject(UserService);

  profileForm!: FormGroup;
  passwordForm!: FormGroup;
  isEditMode = false;
  isChangingPassword = false;
  isLoading = false;
  successMessage = '';
  errorMessage = '';

  ngOnInit(): void {
    const user = this.authService.user()?.user;

    if (!user) {
      this.router.navigate(['/auth/login']);
      return;
    }

    // Initialize forms with cached data first
    this.initializeForms(user);

    // Fetch fresh user data from the server
    this.authService.refreshUser().subscribe({
      next: (freshUser) => {
        // Update forms with fresh data from database
        this.updateFormsWithUserData(freshUser);
      },
      error: (error) => {
        console.error('Failed to refresh user data:', error);
        // Continue using cached data if refresh fails
      }
    });
  }

  private initializeForms(user: any): void {
    this.profileForm = this.fb.group({
      firstName: [user.firstName, [Validators.required]],
      lastName: [user.lastName, [Validators.required]],
      email: [{ value: user.email, disabled: true }],
      phone: [user.phone || ''],
      addressLine1: [user.addressLine1 || ''],
      addressLine2: [user.addressLine2 || ''],
      city: [user.city || ''],
      state: [user.state || ''],
      zipCode: [user.zipCode || ''],
      country: [user.country || 'USA']
    });

    this.passwordForm = this.fb.group({
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]]
    }, { validators: this.passwordMatchValidator });
  }

  private updateFormsWithUserData(user: any): void {
    this.profileForm.patchValue({
      firstName: user.firstName,
      lastName: user.lastName,
      phone: user.phone || '',
      addressLine1: user.addressLine1 || '',
      addressLine2: user.addressLine2 || '',
      city: user.city || '',
      state: user.state || '',
      zipCode: user.zipCode || '',
      country: user.country || 'USA'
    });
  }

  passwordMatchValidator(group: FormGroup): { [key: string]: boolean } | null {
    const password = group.get('newPassword');
    const confirmPassword = group.get('confirmPassword');

    if (!password || !confirmPassword) {
      return null;
    }

    return password.value === confirmPassword.value ? null : { passwordMismatch: true };
  }

  get user() {
    return this.authService.user()?.user;
  }

  toggleEditMode(): void {
    this.isEditMode = !this.isEditMode;
    this.successMessage = '';
    this.errorMessage = '';

    if (!this.isEditMode) {
      // Reset form if canceling edit
      const user = this.user;
      this.profileForm.patchValue({
        firstName: user?.firstName,
        lastName: user?.lastName,
        phone: user?.phone || '',
        addressLine1: user?.addressLine1 || '',
        addressLine2: user?.addressLine2 || '',
        city: user?.city || '',
        state: user?.state || '',
        zipCode: user?.zipCode || '',
        country: user?.country || 'USA'
      });
    }
  }

  togglePasswordChange(): void {
    this.isChangingPassword = !this.isChangingPassword;
    this.successMessage = '';
    this.errorMessage = '';

    if (!this.isChangingPassword) {
      this.passwordForm.reset();
    }
  }

  onSaveProfile(): void {
    if (this.profileForm.invalid) return;

    this.isLoading = true;
    this.errorMessage = '';

    const formValue = this.profileForm.getRawValue();

    this.userService.updateProfile({
      firstName: formValue.firstName,
      lastName: formValue.lastName,
      phone: formValue.phone,
      addressLine1: formValue.addressLine1,
      addressLine2: formValue.addressLine2,
      city: formValue.city,
      state: formValue.state,
      zipCode: formValue.zipCode,
      country: formValue.country
    }).subscribe({
      next: () => {
        this.isLoading = false;
        this.successMessage = 'Profile updated successfully!';
        this.isEditMode = false;

        // Refresh user data
        this.authService.refreshUser().subscribe();

        setTimeout(() => {
          this.successMessage = '';
        }, 3000);
      },
      error: (error) => {
        this.isLoading = false;
        this.errorMessage = error.error?.error || 'Failed to update profile';
      }
    });
  }

  onChangePassword(): void {
    if (this.passwordForm.invalid) return;

    this.isLoading = true;
    this.errorMessage = '';

    const { currentPassword, newPassword } = this.passwordForm.value;

    this.userService.changePassword({
      currentPassword,
      newPassword
    }).subscribe({
      next: () => {
        this.isLoading = false;
        this.successMessage = 'Password changed successfully!';
        this.isChangingPassword = false;
        this.passwordForm.reset();

        setTimeout(() => {
          this.successMessage = '';
        }, 3000);
      },
      error: (error) => {
        this.isLoading = false;
        this.errorMessage = error.error?.error || 'Failed to change password';
      }
    });
  }

  getUserInitials(): string {
    const user = this.user;
    if (!user) return '';
    return `${user.firstName?.charAt(0) || ''}${user.lastName?.charAt(0) || ''}`.toUpperCase();
  }
}
