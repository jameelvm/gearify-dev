using Dapper;
using Gearify.PaymentService.Domain.Entities;
using Npgsql;

namespace Gearify.PaymentService.Infrastructure.Repositories;

public class PostgresPaymentRepository : IPaymentRepository
{
    private readonly string _connectionString;

    public PostgresPaymentRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PaymentDb")
            ?? "Host=localhost;Database=gearify_payments;Username=postgres;Password=postgres";
    }

    public async Task<PaymentTransaction?> GetTransactionByIdAsync(Guid transactionId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<PaymentTransaction>(
            "SELECT * FROM payment_transactions WHERE id = @Id",
            new { Id = transactionId }
        );
    }

    public async Task<PaymentTransaction?> GetTransactionByOrderIdAsync(string orderId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<PaymentTransaction>(
            "SELECT * FROM payment_transactions WHERE order_id = @OrderId ORDER BY created_at DESC LIMIT 1",
            new { OrderId = orderId }
        );
    }

    public async Task<List<PaymentTransaction>> GetTransactionsByTenantAsync(string tenantId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var results = await connection.QueryAsync<PaymentTransaction>(
            "SELECT * FROM payment_transactions WHERE tenant_id = @TenantId ORDER BY created_at DESC",
            new { TenantId = tenantId }
        );
        return results.ToList();
    }

    public async Task CreateTransactionAsync(PaymentTransaction transaction)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync(@"
            INSERT INTO payment_transactions
            (id, tenant_id, order_id, user_id, amount, currency, status, provider, idempotency_key, created_at, updated_at)
            VALUES
            (@Id, @TenantId, @OrderId, @UserId, @Amount, @Currency, @Status, @Provider, @IdempotencyKey, @CreatedAt, @UpdatedAt)",
            transaction
        );
    }

    public async Task UpdateTransactionAsync(PaymentTransaction transaction)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync(@"
            UPDATE payment_transactions
            SET status = @Status,
                provider_transaction_id = @ProviderTransactionId,
                error_message = @ErrorMessage,
                updated_at = @UpdatedAt
            WHERE id = @Id",
            transaction
        );
    }

    public async Task CreateLedgerEntryAsync(PaymentLedgerEntry entry)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync(@"
            INSERT INTO payment_ledger
            (transaction_id, tenant_id, account_type, amount, currency, entry_time, description)
            VALUES
            (@TransactionId, @TenantId, @AccountType, @Amount, @Currency, @EntryTime, @Description)",
            entry
        );
    }

    public async Task<List<PaymentLedgerEntry>> GetLedgerEntriesAsync(Guid transactionId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var results = await connection.QueryAsync<PaymentLedgerEntry>(
            "SELECT * FROM payment_ledger WHERE transaction_id = @TransactionId ORDER BY entry_time",
            new { TransactionId = transactionId }
        );
        return results.ToList();
    }
}
