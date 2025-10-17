-- Gearify Payment Service Database Schema

CREATE TABLE IF NOT EXISTS payment_transactions (
    id UUID PRIMARY KEY,
    tenant_id VARCHAR(100) NOT NULL,
    order_id VARCHAR(100) NOT NULL,
    user_id VARCHAR(100) NOT NULL,
    amount DECIMAL(10, 2) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'USD',
    status VARCHAR(20) NOT NULL,
    provider VARCHAR(20) NOT NULL,
    provider_transaction_id VARCHAR(200),
    idempotency_key VARCHAR(200) UNIQUE,
    error_message TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_payment_transactions_tenant ON payment_transactions(tenant_id);
CREATE INDEX idx_payment_transactions_order ON payment_transactions(order_id);
CREATE INDEX idx_payment_transactions_user ON payment_transactions(user_id);
CREATE INDEX idx_payment_transactions_idempotency ON payment_transactions(idempotency_key);

CREATE TABLE IF NOT EXISTS payment_ledger (
    id SERIAL PRIMARY KEY,
    transaction_id UUID NOT NULL REFERENCES payment_transactions(id),
    tenant_id VARCHAR(100) NOT NULL,
    account_type VARCHAR(20) NOT NULL CHECK (account_type IN ('debit', 'credit')),
    amount DECIMAL(10, 2) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'USD',
    entry_time TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    description TEXT
);

CREATE INDEX idx_payment_ledger_transaction ON payment_ledger(transaction_id);
CREATE INDEX idx_payment_ledger_tenant ON payment_ledger(tenant_id);
CREATE INDEX idx_payment_ledger_entry_time ON payment_ledger(entry_time);

-- Double-entry bookkeeping constraint
CREATE OR REPLACE FUNCTION check_balanced_ledger()
RETURNS TRIGGER AS $$
BEGIN
    -- This is a placeholder for more complex balance checking logic
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER ensure_balanced_ledger
    AFTER INSERT ON payment_ledger
    FOR EACH ROW
    EXECUTE FUNCTION check_balanced_ledger();
