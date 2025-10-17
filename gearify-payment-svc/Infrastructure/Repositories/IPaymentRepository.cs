using Gearify.PaymentService.Domain.Entities;

namespace Gearify.PaymentService.Infrastructure.Repositories;

public interface IPaymentRepository
{
    Task<PaymentTransaction?> GetTransactionByIdAsync(Guid transactionId);
    Task<PaymentTransaction?> GetTransactionByOrderIdAsync(string orderId);
    Task<List<PaymentTransaction>> GetTransactionsByTenantAsync(string tenantId);
    Task CreateTransactionAsync(PaymentTransaction transaction);
    Task UpdateTransactionAsync(PaymentTransaction transaction);
    Task CreateLedgerEntryAsync(PaymentLedgerEntry entry);
    Task<List<PaymentLedgerEntry>> GetLedgerEntriesAsync(Guid transactionId);
}
