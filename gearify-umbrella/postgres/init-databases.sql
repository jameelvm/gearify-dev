-- =============================================
-- Gearify PostgreSQL Database Initialization
-- Creates separate databases for microservices
-- =============================================

-- Create databases for each service
CREATE DATABASE gearify_orders;
CREATE DATABASE gearify_payments;
CREATE DATABASE gearify_shipping;

-- Grant permissions
GRANT ALL PRIVILEGES ON DATABASE gearify_orders TO postgres;
GRANT ALL PRIVILEGES ON DATABASE gearify_payments TO postgres;
GRANT ALL PRIVILEGES ON DATABASE gearify_shipping TO postgres;

-- =============================================
-- Orders Database Schema
-- =============================================
\c gearify_orders

CREATE TABLE IF NOT EXISTS orders (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id VARCHAR(50) NOT NULL,
    user_id VARCHAR(100) NOT NULL,
    order_number VARCHAR(20) NOT NULL UNIQUE,
    status VARCHAR(30) NOT NULL DEFAULT 'pending',
    subtotal DECIMAL(12, 2) NOT NULL,
    tax_amount DECIMAL(12, 2) NOT NULL DEFAULT 0,
    shipping_amount DECIMAL(12, 2) NOT NULL DEFAULT 0,
    discount_amount DECIMAL(12, 2) NOT NULL DEFAULT 0,
    total_amount DECIMAL(12, 2) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'USD',

    -- Shipping Address (ID reference + JSONB snapshot)
    shipping_address_id VARCHAR(100),
    shipping_address JSONB,  -- Snapshot at order time for immutability

    -- Billing Address (ID reference + JSONB snapshot)
    billing_address_id VARCHAR(100),
    billing_address JSONB,  -- Snapshot at order time for immutability

    -- Payment info (references payment service)
    payment_id UUID,
    payment_status VARCHAR(30),

    -- Shipping info (references shipping service)
    shipment_id UUID,
    shipping_status VARCHAR(30),

    -- Saga state
    saga_state VARCHAR(50) DEFAULT 'created',
    saga_step VARCHAR(50),
    saga_error TEXT,

    -- Timestamps
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ,
    cancelled_at TIMESTAMPTZ,

    -- Metadata
    notes TEXT,
    metadata JSONB DEFAULT '{}'::jsonb
);

CREATE TABLE IF NOT EXISTS order_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    product_id VARCHAR(100) NOT NULL,
    product_sku VARCHAR(50) NOT NULL,
    product_name VARCHAR(255) NOT NULL,
    product_image_url TEXT,
    quantity INT NOT NULL CHECK (quantity > 0),
    unit_price DECIMAL(12, 2) NOT NULL,
    discount_amount DECIMAL(12, 2) NOT NULL DEFAULT 0,
    total_price DECIMAL(12, 2) NOT NULL,
    metadata JSONB DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS order_status_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    from_status VARCHAR(30),
    to_status VARCHAR(30) NOT NULL,
    changed_by VARCHAR(100),
    reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indexes for orders
CREATE INDEX idx_orders_tenant_id ON orders(tenant_id);
CREATE INDEX idx_orders_user_id ON orders(user_id);
CREATE INDEX idx_orders_status ON orders(status);
CREATE INDEX idx_orders_order_number ON orders(order_number);
CREATE INDEX idx_orders_created_at ON orders(created_at DESC);
CREATE INDEX idx_orders_saga_state ON orders(saga_state);
CREATE INDEX idx_order_items_order_id ON order_items(order_id);
CREATE INDEX idx_order_items_product_id ON order_items(product_id);
CREATE INDEX idx_order_status_history_order_id ON order_status_history(order_id);

-- =============================================
-- Payments Database Schema
-- =============================================
\c gearify_payments

CREATE TABLE IF NOT EXISTS payments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id VARCHAR(50) NOT NULL,
    order_id UUID NOT NULL,
    user_id VARCHAR(100) NOT NULL,

    -- Payment provider details
    provider VARCHAR(30) NOT NULL, -- stripe, paypal, etc.
    provider_payment_id VARCHAR(255), -- Stripe PaymentIntent ID, PayPal Order ID
    provider_customer_id VARCHAR(255),

    -- Amount
    amount DECIMAL(12, 2) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'USD',

    -- Status
    status VARCHAR(30) NOT NULL DEFAULT 'pending',
    -- pending, processing, succeeded, failed, refunded, partially_refunded, cancelled

    -- Payment method info (tokenized, never store raw card data)
    payment_method_type VARCHAR(30), -- card, paypal, bank_transfer
    payment_method_last4 VARCHAR(4),
    payment_method_brand VARCHAR(30), -- visa, mastercard, amex
    payment_method_exp_month INT,
    payment_method_exp_year INT,

    -- Error handling
    error_code VARCHAR(50),
    error_message TEXT,

    -- Timestamps
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    captured_at TIMESTAMPTZ,

    -- Metadata
    metadata JSONB DEFAULT '{}'::jsonb
);

CREATE TABLE IF NOT EXISTS payment_methods (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id VARCHAR(50) NOT NULL,
    user_id VARCHAR(100) NOT NULL,

    -- Provider details
    provider VARCHAR(30) NOT NULL,
    provider_payment_method_id VARCHAR(255) NOT NULL, -- Stripe PaymentMethod ID
    provider_customer_id VARCHAR(255),

    -- Card info (tokenized)
    type VARCHAR(30) NOT NULL, -- card, bank_account
    brand VARCHAR(30),
    last4 VARCHAR(4),
    exp_month INT,
    exp_year INT,

    -- Billing address
    billing_name VARCHAR(200),
    billing_address_line1 VARCHAR(255),
    billing_address_line2 VARCHAR(255),
    billing_city VARCHAR(100),
    billing_state VARCHAR(100),
    billing_zip_code VARCHAR(20),
    billing_country VARCHAR(100),

    -- Status
    is_default BOOLEAN NOT NULL DEFAULT false,
    is_active BOOLEAN NOT NULL DEFAULT true,

    -- Timestamps
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    UNIQUE(provider, provider_payment_method_id)
);

CREATE TABLE IF NOT EXISTS refunds (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    payment_id UUID NOT NULL REFERENCES payments(id),
    provider_refund_id VARCHAR(255),
    amount DECIMAL(12, 2) NOT NULL,
    reason VARCHAR(255),
    status VARCHAR(30) NOT NULL DEFAULT 'pending',
    -- pending, succeeded, failed
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    processed_at TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS payment_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    payment_id UUID NOT NULL REFERENCES payments(id),
    event_type VARCHAR(50) NOT NULL,
    provider_event_id VARCHAR(255),
    payload JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indexes for payments
CREATE INDEX idx_payments_tenant_id ON payments(tenant_id);
CREATE INDEX idx_payments_order_id ON payments(order_id);
CREATE INDEX idx_payments_user_id ON payments(user_id);
CREATE INDEX idx_payments_status ON payments(status);
CREATE INDEX idx_payments_provider_payment_id ON payments(provider_payment_id);
CREATE INDEX idx_payment_methods_tenant_user ON payment_methods(tenant_id, user_id);
CREATE INDEX idx_payment_methods_provider_customer ON payment_methods(provider_customer_id);
CREATE INDEX idx_refunds_payment_id ON refunds(payment_id);
CREATE INDEX idx_payment_events_payment_id ON payment_events(payment_id);

-- =============================================
-- Shipping Database Schema
-- =============================================
\c gearify_shipping

CREATE TABLE IF NOT EXISTS shipments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id VARCHAR(50) NOT NULL,
    order_id UUID NOT NULL,
    user_id VARCHAR(100) NOT NULL,

    -- Carrier details
    carrier VARCHAR(50), -- fedex, ups, usps, dhl
    service_type VARCHAR(50), -- standard, express, overnight

    -- Tracking
    tracking_number VARCHAR(100),
    tracking_url TEXT,

    -- Status
    status VARCHAR(30) NOT NULL DEFAULT 'pending',
    -- pending, label_created, picked_up, in_transit, out_for_delivery, delivered, failed, returned

    -- Shipping address
    recipient_name VARCHAR(200),
    recipient_phone VARCHAR(20),
    address_line1 VARCHAR(255) NOT NULL,
    address_line2 VARCHAR(255),
    city VARCHAR(100) NOT NULL,
    state VARCHAR(100),
    zip_code VARCHAR(20) NOT NULL,
    country VARCHAR(100) NOT NULL,

    -- Package details
    weight_kg DECIMAL(10, 3),
    length_cm DECIMAL(10, 2),
    width_cm DECIMAL(10, 2),
    height_cm DECIMAL(10, 2),

    -- Cost
    shipping_cost DECIMAL(12, 2),
    insurance_cost DECIMAL(12, 2) DEFAULT 0,

    -- Dates
    estimated_delivery_date DATE,
    actual_delivery_date DATE,
    shipped_at TIMESTAMPTZ,
    delivered_at TIMESTAMPTZ,

    -- Timestamps
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Metadata
    label_url TEXT,
    metadata JSONB DEFAULT '{}'::jsonb
);

CREATE TABLE IF NOT EXISTS shipment_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    shipment_id UUID NOT NULL REFERENCES shipments(id) ON DELETE CASCADE,
    order_item_id UUID NOT NULL,
    product_id VARCHAR(100) NOT NULL,
    product_name VARCHAR(255) NOT NULL,
    quantity INT NOT NULL CHECK (quantity > 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS shipment_tracking_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    shipment_id UUID NOT NULL REFERENCES shipments(id) ON DELETE CASCADE,
    event_type VARCHAR(50) NOT NULL,
    description TEXT,
    location VARCHAR(255),
    occurred_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS shipping_rates (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id VARCHAR(50) NOT NULL,
    carrier VARCHAR(50) NOT NULL,
    service_type VARCHAR(50) NOT NULL,
    origin_country VARCHAR(100),
    destination_country VARCHAR(100),
    min_weight_kg DECIMAL(10, 3) DEFAULT 0,
    max_weight_kg DECIMAL(10, 3),
    base_rate DECIMAL(12, 2) NOT NULL,
    rate_per_kg DECIMAL(12, 2) DEFAULT 0,
    estimated_days_min INT,
    estimated_days_max INT,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indexes for shipping
CREATE INDEX idx_shipments_tenant_id ON shipments(tenant_id);
CREATE INDEX idx_shipments_order_id ON shipments(order_id);
CREATE INDEX idx_shipments_user_id ON shipments(user_id);
CREATE INDEX idx_shipments_status ON shipments(status);
CREATE INDEX idx_shipments_tracking_number ON shipments(tracking_number);
CREATE INDEX idx_shipment_items_shipment_id ON shipment_items(shipment_id);
CREATE INDEX idx_shipment_tracking_events_shipment_id ON shipment_tracking_events(shipment_id);
CREATE INDEX idx_shipping_rates_tenant ON shipping_rates(tenant_id);
CREATE INDEX idx_shipping_rates_lookup ON shipping_rates(carrier, service_type, destination_country);
