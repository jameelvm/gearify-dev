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
    
    // User
    USER_PROFILE: '/api/user/profile',
    USER_ADDRESSES: '/api/user/addresses',
  },
  TIMEOUT: 30000,
};

export const STORAGE_KEYS = {
  ACCESS_TOKEN: 'access_token',
  REFRESH_TOKEN: 'refresh_token',
  USER: 'user',
  CART_ID: 'cart_id',
  TENANT_ID: 'tenant_id',
  THEME: 'theme',
};
