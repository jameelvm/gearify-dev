/**
 * Order models - aligned with backend API (gearify-order-svc)
 */

// API Response models
export interface OrderDto {
  id: string;
  orderNumber: string;
  tenantId: string;
  userId: string;
  status: string;
  items: OrderItemDto[];
  subtotal: number;
  taxAmount: number;
  shippingAmount: number;
  discountAmount: number;
  totalAmount: number;
  currency: string;
  shippingAddress: OrderAddressDto | null;
  billingAddress: OrderAddressDto | null;
  paymentId: string | null;
  paymentStatus: string | null;
  shipmentId: string | null;
  shippingStatus: string | null;
  sagaState: string;
  statusHistory: OrderStatusHistoryDto[];
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
  cancelledAt: string | null;
}

export interface OrderItemDto {
  id: string;
  productId: string;
  productSku: string;
  productName: string;
  productImageUrl: string | null;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  totalPrice: number;
}

export interface OrderAddressDto {
  addressId?: string;
  fullName: string;
  street: string;
  street2?: string;
  city: string;
  state: string;
  postalCode: string;
  country: string;
  phone?: string;
}

export interface OrderStatusHistoryDto {
  fromStatus: string;
  toStatus: string;
  reason: string | null;
  changedBy: string | null;
  createdAt: string;
}

export interface OrderSummaryDto {
  id: string;
  orderNumber: string;
  status: string;
  itemCount: number;
  totalAmount: number;
  currency: string;
  createdAt: string;
}

export interface OrderListResponse {
  orders: OrderSummaryDto[];
  totalCount: number;
}

// Request models
export interface CreateOrderRequest {
  userId: string;
  items: CreateOrderItemRequest[];
  shippingAddress: OrderAddressDto;
  billingAddress?: OrderAddressDto;
  subtotal: number;
  taxAmount: number;
  shippingAmount: number;
  discountAmount: number;
  currency: string;
}

export interface CreateOrderItemRequest {
  productId: string;
  productSku: string;
  productName: string;
  productImageUrl?: string;
  quantity: number;
  unitPrice: number;
}

export interface CancelOrderRequest {
  reason: string;
  cancelledBy?: string;
}

export interface UpdateOrderStatusRequest {
  status: string;
  reason?: string;
  changedBy?: string;
}

// Enums
export enum OrderStatus {
  Pending = 'Pending',
  PaymentProcessing = 'PaymentProcessing',
  PaymentFailed = 'PaymentFailed',
  Paid = 'Paid',
  Processing = 'Processing',
  Shipped = 'Shipped',
  Delivered = 'Delivered',
  Cancelled = 'Cancelled',
  Refunded = 'Refunded'
}

export enum PaymentProvider {
  Stripe = 'Stripe',
  PayPal = 'PayPal'
}
