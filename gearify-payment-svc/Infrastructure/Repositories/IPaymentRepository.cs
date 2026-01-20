using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gearify.PaymentService.Domain.Entities;

namespace Gearify.PaymentService.Infrastructure.Repositories;

public interface IPaymentRepository
{
    // Transaction operations
    Task<PaymentTransaction?> GetTransactionByIdAsync(Guid transactionId);
    Task<PaymentTransaction?> GetTransactionByIdWithDetailsAsync(Guid transactionId);
    Task<PaymentTransaction?> GetTransactionByOrderIdAsync(string orderId);
    Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey);
    Task<List<PaymentTransaction>> GetTransactionsByTenantAsync(string tenantId);
    Task<List<PaymentTransaction>> GetTransactionsByTenantAsync(string tenantId, int skip, int take);
    Task<(List<PaymentTransaction> Transactions, int TotalCount)> GetTransactionsByUserAsync(string tenantId, string userId, int page, int pageSize);
    Task CreateTransactionAsync(PaymentTransaction transaction);
    Task UpdateTransactionAsync(PaymentTransaction transaction);

    // Ledger operations
    Task CreateLedgerEntryAsync(PaymentLedgerEntry entry);
    Task<List<PaymentLedgerEntry>> GetLedgerEntriesAsync(Guid transactionId);

    // Refund operations
    Task<Refund?> GetRefundByIdAsync(Guid refundId);
    Task<List<Refund>> GetRefundsByTransactionIdAsync(Guid transactionId);
    Task CreateRefundAsync(Refund refund);
    Task UpdateRefundAsync(Refund refund);
    Task<decimal> GetTotalRefundedAmountAsync(Guid transactionId);
}
