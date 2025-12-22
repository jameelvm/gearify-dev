import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { CommonModule } from '@angular/common';
import { filter } from 'rxjs';
import { ThemeService } from '@core/services/theme.service';
import { isMobileDevice, isTouchDevice } from '@shared/utils/device.utils';
import { STORAGE_KEYS } from '@shared/constants/api.constants';
import { NavbarComponent } from '@shared/components/navbar/navbar.component';
import { CategoryNavComponent } from '@shared/components/category-nav/category-nav.component';
import { FooterComponent } from '@shared/components/footer/footer.component';

/**
 * Root application component
 * Detects device type and loads appropriate shell (mobile/desktop)
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, NavbarComponent, CategoryNavComponent, FooterComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  private themeService = inject(ThemeService);
  private router = inject(Router);

  isMobile = signal(false);
  isTouch = signal(false);
  currentRoute = signal('');

  ngOnInit(): void {
    this.initializeTenantId();
    this.detectDevice();
    this.setupResizeListener();
    this.trackRoute();
  }

  private trackRoute(): void {
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe((event: any) => {
        this.currentRoute.set(event.urlAfterRedirects || event.url);
      });
  }

  shouldShowLayout(): boolean {
    const route = this.currentRoute();
    return !route.includes('/tenant-not-found') && !route.includes('/auth/');
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
