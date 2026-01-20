using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gearify.PaymentService.Domain.Entities;
using Gearify.PaymentService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Gearify.PaymentService.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _context;

    public PaymentRepository(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentTransaction?> GetTransactionByIdAsync(Guid transactionId)
    {
        return await _context.PaymentTransactions
            .FirstOrDefaultAsync(t => t.Id == transactionId);
    }

    public async Task<PaymentTransaction?> GetTransactionByIdWithDetailsAsync(Guid transactionId)
    {
        return await _context.PaymentTransactions
            .Include(t => t.LedgerEntries)
            .Include(t => t.Refunds)
            .FirstOrDefaultAsync(t => t.Id == transactionId);
    }

    public async Task<PaymentTransaction?> GetTransactionByOrderIdAsync(string orderId)
    {
        return await _context.PaymentTransactions
            .Where(t => t.OrderId == orderId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<PaymentTransaction>> GetTransactionsByTenantAsync(string tenantId)
    {
        return await _context.PaymentTransactions
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<PaymentTransaction>> GetTransactionsByTenantAsync(
        string tenantId,
        int skip,
        int take)
    {
        return await _context.PaymentTransactions
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<(List<PaymentTransaction> Transactions, int TotalCount)> GetTransactionsByUserAsync(
        string tenantId,
        string userId,
        int page,
        int pageSize)
    {
        var query = _context.PaymentTransactions
            .Where(t => t.TenantId == tenantId && t.UserId == userId);

        var totalCount = await query.CountAsync();

        var transactions = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(t => t.Refunds)
            .ToListAsync();

        return (transactions, totalCount);
    }

    public async Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey)
    {
        return await _context.PaymentTransactions
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey);
    }

    public Task CreateTransactionAsync(PaymentTransaction transaction)
    {
        _context.PaymentTransactions.Add(transaction);
        return Task.CompletedTask;
    }

    public Task UpdateTransactionAsync(PaymentTransaction transaction)
    {
        transaction.UpdatedAt = DateTime.UtcNow;
        _context.PaymentTransactions.Update(transaction);
        return Task.CompletedTask;
    }

    public Task CreateLedgerEntryAsync(PaymentLedgerEntry entry)
    {
        _context.PaymentLedgerEntries.Add(entry);
        return Task.CompletedTask;
    }

    public async Task<List<PaymentLedgerEntry>> GetLedgerEntriesAsync(Guid transactionId)
    {
        return await _context.PaymentLedgerEntries
            .Where(e => e.TransactionId == transactionId)
            .OrderBy(e => e.EntryTime)
            .ToListAsync();
    }

    public async Task<Refund?> GetRefundByIdAsync(Guid refundId)
    {
        return await _context.Refunds
            .FirstOrDefaultAsync(r => r.Id == refundId);
    }

    public async Task<List<Refund>> GetRefundsByTransactionIdAsync(Guid transactionId)
    {
        return await _context.Refunds
            .Where(r => r.TransactionId == transactionId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public Task CreateRefundAsync(Refund refund)
    {
        _context.Refunds.Add(refund);
        return Task.CompletedTask;
    }

    public Task UpdateRefundAsync(Refund refund)
    {
        _context.Refunds.Update(refund);
        return Task.CompletedTask;
    }

    public async Task<decimal> GetTotalRefundedAmountAsync(Guid transactionId)
    {
        return await _context.Refunds
            .Where(r => r.TransactionId == transactionId && r.Status == RefundStatus.Succeeded)
            .SumAsync(r => r.Amount);
    }
}
