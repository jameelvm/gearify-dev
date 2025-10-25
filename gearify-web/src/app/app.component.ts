import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from '@core/services/theme.service';
import { isMobileDevice, isTouchDevice } from '@shared/utils/device.utils';
import { STORAGE_KEYS } from '@shared/constants/api.constants';

/**
 * Root application component
 * Detects device type and loads appropriate shell (mobile/desktop)
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  private themeService = inject(ThemeService);

  isMobile = signal(false);
  isTouch = signal(false);

  ngOnInit(): void {
    this.initializeTenantId();
    this.detectDevice();
    this.setupResizeListener();
  }

  private initializeTenantId(): void {
    if (typeof localStorage !== 'undefined' && !localStorage.getItem(STORAGE_KEYS.TENANT_ID)) {
      localStorage.setItem(STORAGE_KEYS.TENANT_ID, 'default');
    }
  }

  private detectDevice(): void {
    this.isMobile.set(isMobileDevice());
    this.isTouch.set(isTouchDevice());
  }

  private setupResizeListener(): void {
    if (typeof window !== 'undefined') {
      window.addEventListener('resize', () => this.detectDevice());
    }
  }
}
