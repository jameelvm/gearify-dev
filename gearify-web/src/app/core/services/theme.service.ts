import { Injectable, signal, effect } from '@angular/core';
import { STORAGE_KEYS } from '@shared/constants/api.constants';

export type Theme = 'light' | 'dark' | 'auto';

/**
 * Theme management service
 * Handles light/dark mode switching with CSS variables
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private themeSignal = signal<Theme>(this.loadThemeFromStorage());
  public theme = this.themeSignal.asReadonly();

  constructor() {
    // Apply theme on initialization
    this.applyTheme(this.themeSignal());

    // Watch for theme changes
    effect(() => {
      const theme = this.themeSignal();
      this.applyTheme(theme);
      localStorage.setItem(STORAGE_KEYS.THEME, theme);
    });

    // Watch for system theme changes when in auto mode
    if (typeof window !== 'undefined') {
      window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
        if (this.themeSignal() === 'auto') {
          this.applyTheme('auto');
        }
      });
    }
  }

  setTheme(theme: Theme): void {
    this.themeSignal.set(theme);
  }

  toggleTheme(): void {
    const current = this.themeSignal();
    this.themeSignal.set(current === 'light' ? 'dark' : 'light');
  }

  private applyTheme(theme: Theme): void {
    if (typeof document === 'undefined') return;

    const isDark = theme === 'dark' ||
      (theme === 'auto' && window.matchMedia('(prefers-color-scheme: dark)').matches);

    document.documentElement.setAttribute('data-theme', isDark ? 'dark' : 'light');
    document.body.classList.toggle('dark-theme', isDark);
  }

  private loadThemeFromStorage(): Theme {
    if (typeof localStorage === 'undefined') return 'light';
    const stored = localStorage.getItem(STORAGE_KEYS.THEME) as Theme;
    return stored || 'light';
  }
}
