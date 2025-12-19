import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '@features/auth/auth.service';

@Component({
  selector: 'app-verify-email',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './verify-email.component.html',
  styleUrls: ['./verify-email.component.scss']
})
export class VerifyEmailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private authService = inject(AuthService);
  private router = inject(Router);

  isVerifying = true;
  verificationSuccess = false;
  errorMessage = '';

  ngOnInit(): void {
    // Get the token from query parameters
    const token = this.route.snapshot.queryParamMap.get('token');

    if (!token) {
      this.isVerifying = false;
      this.errorMessage = 'Invalid verification link. No token provided.';
      return;
    }

    // Call the verification API
    this.authService.verifyEmail(token).subscribe({
      next: () => {
        this.isVerifying = false;
        this.verificationSuccess = true;
        // Redirect to login after 3 seconds
        setTimeout(() => {
          this.router.navigate(['/auth/login']);
        }, 3000);
      },
      error: (error) => {
        this.isVerifying = false;
        this.verificationSuccess = false;
        this.errorMessage = error.error?.error || error.error?.message || 'Email verification failed. The link may have expired or is invalid.';
      }
    });
  }
}
