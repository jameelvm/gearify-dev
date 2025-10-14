-- Create payment_transactions table
CREATE TABLE IF NOT EXISTS payment_transactions (
    id SERIAL PRIMARY KEY,
    tenant_id VARCHAR(50) NOT NULL,
    order_id VARCHAR(100) NOT NULL,
    payment_provider VARCHAR(20) NOT NULL,
    payment_intent_id VARCHAR(255),
    amount DECIMAL(10, 2) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'USD',
    status VARCHAR(20) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_payment_tenant ON payment_transactions(tenant_id);
CREATE INDEX IF NOT EXISTS idx_payment_order ON payment_transactions(order_id);
CREATE INDEX IF NOT EXISTS idx_payment_status ON payment_transactions(status);

-- Seed sample payment records
INSERT INTO payment_transactions (tenant_id, order_id, payment_provider, payment_intent_id, amount, currency, status)
VALUES
    ('default', 'order-001', 'stripe', 'pi_test_123', 299.99, 'USD', 'succeeded'),
    ('default', 'order-002', 'paypal', 'paypal_test_456', 149.50, 'USD', 'succeeded'),
    ('global-demo', 'order-003', 'stripe', 'pi_test_789', 89.99, 'USD', 'pending')
ON CONFLICT DO NOTHING;
