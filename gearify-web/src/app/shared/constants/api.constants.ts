import { environment } from '@environments/environment';

export const API_CONFIG = {
  BASE_URL: environment.apiUrl,
  ENDPOINTS: {
    // Auth
    LOGIN: '/api/auth/login',
    REGISTER: '/api/auth/register',
    REFRESH_TOKEN: '/api/auth/refresh',
    LOGOUT: '/api/auth/logout',
    ME: '/api/auth/me',
    VERIFY_EMAIL: '/api/auth/verify-email',
    FORGOT_PASSWORD: '/api/password/forgot',
    RESET_PASSWORD: '/api/password/reset',
    
    // Catalog
    CATALOG: '/api/catalog',
    DEPARTMENTS: '/api/catalog/departments',
    DEPARTMENT_BY_SLUG: (slug: string) => `/api/catalog/departments/${slug}`,
    DEPARTMENT_CATEGORIES: (slug: string) => `/api/catalog/departments/${slug}/categories`,
    BRANDS: '/api/catalog/brands',
    PRICE_RANGES: '/api/catalog/price-ranges',
    SORT_OPTIONS: '/api/catalog/sort-options',

    // Products
    PRODUCTS: '/api/catalog/products',
    PRODUCT_BY_ID: (id: string) => `/api/catalog/products/${id}`,
    PRODUCTS_BY_CATEGORY: (category: string) => `/api/catalog/products?category=${category}`,
    
    // Cart
    CART: '/api/cart',
    CART_ITEMS: '/api/cart/items',
    CART_ITEM_BY_ID: (id: string) => `/api/cart/items/${id}`,
    
    // Orders
    ORDERS: '/api/orders',
    ORDER_BY_ID: (id: string) => `/api/orders/${id}`,
    ORDER_BY_NUMBER: (orderNumber: string) => `/api/orders/by-number/${orderNumber}`,
    CANCEL_ORDER: (id: string) => `/api/orders/${id}/cancel`,
    UPDATE_ORDER_STATUS: (id: string) => `/api/orders/${id}/status`,

    // Payments
    PAYMENTS: '/api/payments',
    PAYMENT_BY_ID: (id: string) => `/api/payments/${id}`,
    PAYMENT_BY_ORDER: (orderId: string) => `/api/payments/order/${orderId}`,
    REFUND_PAYMENT: (id: string) => `/api/payments/${id}/refunds`,
    
    // User
    USER_PROFILE: '/api/user/profile',
    UPDATE_PROFILE: '/api/user/profile',
    CHANGE_PASSWORD: '/api/user/change-password',

    // Address
    ADDRESSES: '/api/address',
    ADDRESS_BY_ID: (id: string) => `/api/address/${id}`,

    // Search
    SEARCH_PRODUCTS: '/api/search/products',
    SEARCH_AUTOCOMPLETE: '/api/search/autocomplete',
    SEARCH_SIMILAR: (productId: string) => `/api/search/similar/${productId}`,
  },
  TIMEOUT: 30000,
};

export const STORAGE_KEYS = {
  ACCESS_TOKEN: 'access_token',
  REFRESH_TOKEN: 'refresh_token',
  USER: 'user',
  CART_ID: 'cart_id',
  SESSION_ID: 'session_id',
  TENANT_ID: 'tenant_id',
  THEME: 'theme',
};
