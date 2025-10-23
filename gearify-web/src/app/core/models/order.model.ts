/**
 * Order models
 */
export interface Order {
  id: string;
  tenantId: string;
  userId: string;
  orderNumber: string;
  status: OrderStatus;
  items: OrderItem[];
  shippingAddress: Address;
  billingAddress: Address;
  subtotal: number;
  tax: number;
  shipping: number;
  discount: number;
  total: number;
  currency: string;
  paymentMethod: PaymentMethod;
  paymentStatus: PaymentStatus;
  shippingMethod: string;
  trackingNumber?: string;
  notes?: string;
  createdAt: Date;
  updatedAt: Date;
}

export interface OrderItem {
  id: string;
  productId: string;
  productName: string;
  productSku: string;
  quantity: number;
  price: number;
  subtotal: number;
  imageUrl: string;
}

export interface Address {
  firstName: string;
  lastName: string;
  company?: string;
  address1: string;
  address2?: string;
  city: string;
  state: string;
  postalCode: string;
  country: string;
  phone: string;
}

export enum OrderStatus {
  Pending = 'Pending',
  Processing = 'Processing',
  Shipped = 'Shipped',
  Delivered = 'Delivered',
  Cancelled = 'Cancelled',
  Refunded = 'Refunded'
}

export enum PaymentMethod {
  CreditCard = 'CreditCard',
  PayPal = 'PayPal',
  Stripe = 'Stripe'
}

export enum PaymentStatus {
  Pending = 'Pending',
  Authorized = 'Authorized',
  Captured = 'Captured',
  Failed = 'Failed',
  Refunded = 'Refunded'
}
