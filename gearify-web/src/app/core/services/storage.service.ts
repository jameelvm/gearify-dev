import { Injectable } from '@angular/core';
import { STORAGE_KEYS } from '@shared/constants/api.constants';
import { User, AuthTokens } from '../models/user.model';

/**
 * Storage Service
 * Centralized service for all localStorage operations
 * Handles SSR compatibility and encapsulates storage logic
 */
@Injectable({ providedIn: 'root' })
export class StorageService {
  /**
   * Check if localStorage is available (SSR compatible)
   */
  private isLocalStorageAvailable(): boolean {
    return typeof localStorage !== 'undefined';
  }

  // ==================== Tenant Management ====================

  /**
   * Get tenant ID from localStorage
   */
  getTenantId(): string | null {
    if (!this.isLocalStorageAvailable()) return null;
    return localStorage.getItem(STORAGE_KEYS.TENANT_ID);
  }

  /**
   * Set tenant ID in localStorage
   */
  setTenantId(tenantId: string): void {
    if (!this.isLocalStorageAvailable()) return;
    localStorage.setItem(STORAGE_KEYS.TENANT_ID, tenantId);
  }

  /**
   * Remove tenant ID from localStorage
   */
  removeTenantId(): void {
    if (!this.isLocalStorageAvailable()) return;
    localStorage.removeItem(STORAGE_KEYS.TENANT_ID);
  }

  // ==================== Token Management ====================

  /**
   * Get access token from localStorage
   */
  getAccessToken(): string | null {
    if (!this.isLocalStorageAvailable()) return null;
    return localStorage.getItem(STORAGE_KEYS.ACCESS_TOKEN);
  }

  /**
   * Set access token in localStorage
   */
  setAccessToken(token: string): void {
    if (!this.isLocalStorageAvailable()) return;
    localStorage.setItem(STORAGE_KEYS.ACCESS_TOKEN, token);
  }

  /**
   * Remove access token from localStorage
   */
  removeAccessToken(): void {
    if (!this.isLocalStorageAvailable()) return;
    localStorage.removeItem(STORAGE_KEYS.ACCESS_TOKEN);
  }

  /**
   * Get refresh token from localStorage
   */
  getRefreshToken(): string | null {
    if (!this.isLocalStorageAvailable()) return null;
    return localStorage.getItem(STORAGE_KEYS.REFRESH_TOKEN);
  }

  /**
   * Set refresh token in localStorage
   */
  setRefreshToken(token: string): void {
    if (!this.isLocalStorageAvailable()) return;
    localStorage.setItem(STORAGE_KEYS.REFRESH_TOKEN, token);
  }

  /**
   * Remove refresh token from localStorage
   */
  removeRefreshToken(): void {
    if (!this.isLocalStorageAvailable()) return;
    localStorage.removeItem(STORAGE_KEYS.REFRESH_TOKEN);
  }

  /**
   * Get both access and refresh tokens
   */
  getTokens(): AuthTokens | null {
    if (!this.isLocalStorageAvailable()) return null;

    const accessToken = this.getAccessToken();
    const refreshToken = this.getRefreshToken();

    if (accessToken && refreshToken) {
      return { accessToken, refreshToken, expiresIn: 3600 };
    }
    return null;
  }

  /**
   * Set both access and refresh tokens
   */
  setTokens(tokens: AuthTokens): void {
    if (!this.isLocalStorageAvailable()) return;

    if (tokens.accessToken) {
      this.setAccessToken(tokens.accessToken);
    }
    if (tokens.refreshToken) {
      this.setRefreshToken(tokens.refreshToken);
    }
  }

  /**
   * Remove all tokens from localStorage
   */
  removeTokens(): void {
    this.removeAccessToken();
    this.removeRefreshToken();
  }

  // ==================== User Management ====================

  /**
   * Get user from localStorage
   */
  getUser(): User | null {
    if (!this.isLocalStorageAvailable()) return null;

    const userStr = localStorage.getItem(STORAGE_KEYS.USER);
    if (!userStr) return null;

    try {
      return JSON.parse(userStr);
    } catch (error) {
      console.error('Failed to parse user from localStorage:', error);
      return null;
    }
  }

  /**
   * Set user in localStorage
   */
  setUser(user: User): void {
    if (!this.isLocalStorageAvailable()) return;
    localStorage.setItem(STORAGE_KEYS.USER, JSON.stringify(user));
  }

  /**
   * Remove user from localStorage
   */
  removeUser(): void {
    if (!this.isLocalStorageAvailable()) return;
    localStorage.removeItem(STORAGE_KEYS.USER);
  }

  // ==================== Composite Operations ====================

  /**
   * Set all authentication data (user + tokens)
   */
  setAuthData(user: User, tokens: AuthTokens): void {
    this.setUser(user);
    this.setTokens(tokens);
  }

  /**
   * Clear all authentication data (user + tokens)
   */
  clearAuthData(): void {
    this.removeUser();
    this.removeTokens();
  }

  /**
   * Clear all application data (auth + tenant)
   */
  clearAllData(): void {
    this.clearAuthData();
    this.removeTenantId();
  }

  /**
   * Check if user is authenticated (has valid tokens)
   */
  isAuthenticated(): boolean {
    return !!this.getAccessToken() && !!this.getRefreshToken();
  }
}
