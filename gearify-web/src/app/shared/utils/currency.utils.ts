/**
 * Currency formatting utilities
 */
export class CurrencyUtils {
  static format(amount: number, currency: string = 'USD', locale: string = 'en-US'): string {
    return new Intl.NumberFormat(locale, {
      style: 'currency',
      currency,
    }).format(amount);
  }

  static formatCompact(amount: number, currency: string = 'USD', locale: string = 'en-US'): string {
    return new Intl.NumberFormat(locale, {
      style: 'currency',
      currency,
      notation: 'compact',
    }).format(amount);
  }

  static calculateDiscount(originalPrice: number, salePrice: number): number {
    if (originalPrice <= 0) return 0;
    return Math.round(((originalPrice - salePrice) / originalPrice) * 100);
  }

  static calculateTax(subtotal: number, taxRate: number): number {
    return Number((subtotal * taxRate).toFixed(2));
  }
}

/**
 * Standalone function for formatting currency
 * @param amount - The amount to format
 * @param currency - Currency code (default: 'USD')
 * @param locale - Locale string (default: 'en-US')
 * @returns Formatted currency string
 */
export function formatCurrency(amount: number, currency: string = 'USD', locale: string = 'en-US'): string {
  return CurrencyUtils.format(amount, currency, locale);
}
