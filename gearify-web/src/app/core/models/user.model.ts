/**
 * User and authentication models
 */
export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phone?: string;
  role: UserRole | string;
  isActive: boolean;
  emailVerified?: boolean;
  lastLoginAt?: string | null;
  tenantId?: string;
  isEmailVerified?: boolean;
  defaultShippingAddress?: {
    address1: string;
    address2?: string;
    city: string;
    state: string;
    postalCode: string;
    country: string;
  };
  createdAt?: Date;
  updatedAt?: Date;
}

export enum UserRole {
  Customer = 'Customer',
  Admin = 'Admin',
  Manager = 'Manager',
  SuperAdmin = 'SuperAdmin'
}

export interface AuthTokens {
  accessToken?: string;
  refreshToken?: string;
  expiresIn?: number;
  token?: string;
}

export interface LoginRequest {
  email: string;
  password: string;
  rememberMe?: boolean;
}

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  role?: string;
  phone?: string;
}

export interface AuthState {
  user: User | null;
  tokens: AuthTokens | null;
  isAuthenticated: boolean;
  isLoading: boolean;
}
