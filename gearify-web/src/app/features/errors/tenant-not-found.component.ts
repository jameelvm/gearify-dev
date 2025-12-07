import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { StorageService } from '@core/services/storage.service';

@Component({
  selector: 'app-tenant-not-found',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tenant-not-found.component.html',
  styleUrls: ['./tenant-not-found.component.scss']
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
