import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { StorageService } from '@core/services/storage.service';

@Component({
  selector: 'app-tenant-not-found',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="error-container">
      <div class="error-content">
        <div class="error-icon">🔒</div>
        <h1 class="error-title">Access Not Available</h1>
        <p class="error-message" *ngIf="reason === 'invalid' && tenantId">
          We couldn't find an active store at this address.
        </p>
        <p class="error-message" *ngIf="reason === 'error' && tenantId">
          We're having trouble connecting to our services. Please try again in a moment.
        </p>
        <p class="error-message" *ngIf="!tenantId || tenantId === 'unknown'">
          This URL doesn't appear to be valid. Please check the address and try again.
        </p>
        <p class="error-hint">
          Please verify your web address or contact support for assistance.
        </p>
        <div class="error-actions">
          <button (click)="clearAndReload()" class="btn btn-secondary">
            Clear Cache & Reload
          </button>
        </div>
        <div class="error-details">
          <p class="text-muted">Need help? Contact our support team</p>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .error-container {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      padding: 2rem;
    }

    .error-content {
      background: white;
      border-radius: 1rem;
      padding: 3rem;
      max-width: 600px;
      text-align: center;
      box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
    }

    .error-icon {
      font-size: 5rem;
      margin-bottom: 1rem;
    }

    .error-title {
      font-size: 2.5rem;
      font-weight: 700;
      color: #2d3748;
      margin-bottom: 1rem;
    }

    .error-message {
      font-size: 1.25rem;
      color: #4a5568;
      margin-bottom: 1rem;
    }

    .error-hint {
      font-size: 1rem;
      color: #718096;
      margin-bottom: 2rem;
    }

    .error-actions {
      display: flex;
      gap: 1rem;
      justify-content: center;
      margin-bottom: 2rem;
    }

    .btn {
      padding: 0.75rem 1.5rem;
      border-radius: 0.5rem;
      font-weight: 600;
      text-decoration: none;
      cursor: pointer;
      border: none;
      font-size: 1rem;
      transition: all 0.3s ease;
    }

    .btn-primary {
      background: #667eea;
      color: white;
    }

    .btn-primary:hover {
      background: #5568d3;
      transform: translateY(-2px);
      box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
    }

    .btn-secondary {
      background: #e2e8f0;
      color: #2d3748;
    }

    .btn-secondary:hover {
      background: #cbd5e0;
      transform: translateY(-2px);
    }

    .error-details {
      margin-top: 2rem;
      padding-top: 2rem;
      border-top: 1px solid #e2e8f0;
    }

    .text-muted {
      color: #a0aec0;
      font-size: 0.875rem;
      margin: 0.5rem 0;
    }

    .tenant-list {
      list-style: none;
      padding: 0;
      margin-top: 1rem;
    }

    .tenant-list li {
      color: #667eea;
      font-family: monospace;
      font-size: 0.875rem;
      padding: 0.25rem 0;
    }

    strong {
      color: #e53e3e;
      font-family: monospace;
    }
  `]
})
export class TenantNotFoundComponent implements OnInit {
  tenantId: string = '';
  currentUrl: string = '';
  reason: string = '';
  errorMessage: string = '';

  constructor(
    private router: Router,
    private storage: StorageService
  ) {
    // Get navigation state
    const navigation = this.router.getCurrentNavigation();
    if (navigation?.extras?.state) {
      this.tenantId = navigation.extras.state['tenantId'] || '';
      this.reason = navigation.extras.state['reason'] || '';
      this.errorMessage = navigation.extras.state['errorMessage'] || '';
    }
  }

  ngOnInit() {
    if (typeof window !== 'undefined') {
      this.currentUrl = window.location.href;

      // If no state provided, get from localStorage
      if (!this.tenantId) {
        this.tenantId = this.storage.getTenantId() || 'unknown';
      }
    }
  }

  clearAndReload() {
    this.storage.clearAllData();
    window.location.reload();
  }
}
