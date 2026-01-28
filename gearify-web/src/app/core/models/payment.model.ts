/**
 * Payment models - aligned with backend API (gearify-payment-svc)
 */

// API Response models
export interface PaymentDto {
  id: string;
  tenantId: string;
  orderId: string;
  userId: string;
  amount: number;
  currency: string;
  status: string;
  provider: string;
  providerTransactionId: string | null;
  idempotencyKey: string | null;
  errorMessage: string | null;
  refunds: RefundDto[];
  createdAt: string;
  updatedAt: string;
}

export interface PaymentSummaryDto {
  id: string;
  orderId: string;
  amount: number;
  currency: string;
  status: string;
  provider: string;
  createdAt: string;
}

export interface RefundDto {
  id: string;
  transactionId: string;
  amount: number;
  currency: string;
  status: string;
  providerRefundId: string | null;
  reason: string;
  createdAt: string;
  completedAt: string | null;
}

export interface PaymentListDto {
  payments: PaymentSummaryDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// Request models
export interface ProcessRefundRequest {
  amount: number;
  reason: string;
}

// Enums
export enum PaymentStatus {
  Pending = 'Pending',
  Processing = 'Processing',
  Succeeded = 'Succeeded',
  Failed = 'Failed',
  Refunded = 'Refunded',
  PartiallyRefunded = 'PartiallyRefunded',
  Cancelled = 'Cancelled'
}

export enum RefundStatus {
  Pending = 'Pending',
  Processing = 'Processing',
  Succeeded = 'Succeeded',
  Failed = 'Failed'
}
