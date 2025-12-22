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
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  state?: string | null;
  zipCode?: string | null;
  country?: string | null;
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
