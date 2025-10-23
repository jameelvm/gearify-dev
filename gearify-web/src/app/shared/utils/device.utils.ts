import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { inject } from '@angular/core';
import { Observable, map } from 'rxjs';

/**
 * Device detection utilities
 * Note: DeviceUtils class methods return Observables and require injection context.
 * For SSR-safe boolean detection, use isMobileDevice() and isTouchDevice() functions.
 */
export class DeviceUtils {
  /**
   * Check if the current device is mobile (Observable)
   * Must be called from an injection context
   */
  static isMobile(): Observable<boolean> {
    const breakpointObserver = inject(BreakpointObserver);
    return breakpointObserver
      .observe([Breakpoints.Handset, Breakpoints.TabletPortrait])
      .pipe(map(result => result.matches));
  }

  /**
   * Check if the current device is desktop (Observable)
   * Must be called from an injection context
   */
  static isDesktop(): Observable<boolean> {
    const breakpointObserver = inject(BreakpointObserver);
    return breakpointObserver
      .observe([Breakpoints.Web, Breakpoints.TabletLandscape])
      .pipe(map(result => result.matches));
  }

  /**
   * Check if the current device is tablet (Observable)
   * Must be called from an injection context
   */
  static isTablet(): Observable<boolean> {
    const breakpointObserver = inject(BreakpointObserver);
    return breakpointObserver
      .observe([Breakpoints.Tablet])
      .pipe(map(result => result.matches));
  }
}

/**
 * SSR-safe function to detect mobile devices
 * Returns false during SSR, actual detection in browser
 */
export function isMobileDevice(): boolean {
  if (typeof navigator === 'undefined') return false;
  return /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);
}

/**
 * SSR-safe function to detect touch devices
 * Returns false during SSR, actual detection in browser
 */
export function isTouchDevice(): boolean {
  if (typeof window === 'undefined') return false;
  return 'ontouchstart' in window || (navigator.maxTouchPoints > 0);
}
