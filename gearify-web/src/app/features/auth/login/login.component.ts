import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '@features/auth/auth.service';
import { CartService } from '@core/services/cart.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private cartService = inject(CartService);
  private router = inject(Router);

  loginForm: FormGroup;
  isLoading = false;
  errorMessage = '';

  constructor() {
    this.loginForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required]],
      rememberMe: [false]
    });
  }

  onSubmit(): void {
    if (this.loginForm.invalid) return;

    this.isLoading = true;
    this.errorMessage = '';

    const { email, password, rememberMe } = this.loginForm.value;

    this.authService.login({ email, password, rememberMe }).subscribe({
      next: (response) => {
        // Merge guest cart into user cart
        this.cartService.mergeCart(response.user.id).subscribe({
          next: () => {
            this.isLoading = false;
            this.router.navigate(['/home']);
          },
          error: () => {
            // Still navigate even if merge fails
            this.isLoading = false;
            this.router.navigate(['/home']);
          }
        });
      },
      error: (error) => {
        this.isLoading = false;
        this.errorMessage = error.error?.message || error.error?.error || 'Login failed. Please check your credentials.';
      }
    });
  }
}
