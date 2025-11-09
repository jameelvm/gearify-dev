import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { STORAGE_KEYS } from '@shared/constants/api.constants';

@Component({
  selector: 'app-tenant-not-found',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="error-container">
      <div class="error-content">
        <div class="error-icon">🏢</div>
        <h1 class="error-title">Tenant Not Found</h1>
        <p class="error-message" *ngIf="tenantId && tenantId !== 'unknown'">
          The tenant <strong>{{ tenantId }}</strong> does not exist or is not active.
        </p>
        <p class="error-message" *ngIf="!tenantId || tenantId === 'unknown'">
          No tenant found in URL. Please use a subdomain to access the application.
        </p>
        <p class="error-hint">
          You must access the application using a tenant subdomain.
        </p>
        <div class="error-actions">
          <button (click)="clearAndReload()" class="btn btn-secondary">
            Clear Cache & Reload
          </button>
        </div>
        <div class="error-details">
          <p class="text-muted">Current URL: {{ currentUrl }}</p>
          <p class="text-muted"><strong>Available tenants (development):</strong></p>
          <ul class="tenant-list">
            <li><a href="http://default.localhost.direct:4200">default.localhost.direct:4200</a></li>
            <li><a href="http://acme.localhost.direct:4200">acme.localhost.direct:4200</a></li>
            <li><a href="http://contoso.localhost.direct:4200">contoso.localhost.direct:4200</a></li>
            <li><a href="http://fabrikam.localhost.direct:4200">fabrikam.localhost.direct:4200</a></li>
            <li><a href="http://demo.localhost.direct:4200">demo.localhost.direct:4200</a></li>
          </ul>
          <p class="text-muted" style="margin-top: 1rem;">
            ⚠️ <strong>Note:</strong> Plain <code>localhost:4200</code> will not work - you must use a subdomain.
          </p>
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

  ngOnInit() {
    if (typeof window !== 'undefined') {
      this.currentUrl = window.location.href;
      this.tenantId = localStorage.getItem(STORAGE_KEYS.TENANT_ID) || 'unknown';
    }
  }

  clearAndReload() {
    if (typeof localStorage !== 'undefined') {
      localStorage.removeItem(STORAGE_KEYS.TENANT_ID);
      localStorage.removeItem(STORAGE_KEYS.ACCESS_TOKEN);
      localStorage.removeItem(STORAGE_KEYS.REFRESH_TOKEN);
      localStorage.removeItem(STORAGE_KEYS.USER);
    }
    window.location.reload();
  }
}
