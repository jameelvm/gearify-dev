# Tax & VAT Handling

## Overview
Gearify uses Stripe Tax for automatic tax calculation at checkout.

## Stripe Tax Integration

### Configuration
```csharp
var options = new PaymentIntentCreateOptions
{
    Amount = 29999, // $299.99
    Currency = "usd",
    AutomaticTax = new PaymentIntentAutomaticTaxOptions
    {
        Enabled = true,
    },
    Customer = customerId,
    Metadata = new Dictionary<string, string>
    {
        { "tenantId", "default" },
        { "orderId", "order-123" }
    }
};
```

### Tax Calculation
Stripe automatically:
- Determines customer location
- Applies correct tax rates
- Handles multi-jurisdictional complexity
- Provides tax breakdown in receipt

## Merchant Responsibilities

### 1. Tax Registration
Register for tax collection in jurisdictions where required:
- United States: State sales tax
- European Union: VAT registration if exceeding threshold (€10,000)
- United Kingdom: VAT registration
- Canada: GST/HST/PST registration
- Australia: GST registration

### 2. Tax Reporting
- File periodic tax returns (monthly/quarterly/annually)
- Report collected taxes to authorities
- Remit taxes on schedule

### 3. Record Keeping
- Maintain transaction records for 7+ years
- Store tax invoices
- Document exemptions

## PayPal Tax Handling

PayPal does not automatically calculate tax. Options:

### Option 1: Pre-calculate Tax
```csharp
var taxAmount = CalculateTax(subtotal, customerLocation);
var order = new OrderRequest
{
    PurchaseUnits = new List<PurchaseUnitRequest>
    {
        new PurchaseUnitRequest
        {
            AmountWithBreakdown = new AmountWithBreakdown
            {
                CurrencyCode = "USD",
                Value = (subtotal + taxAmount).ToString("F2"),
                AmountBreakdown = new AmountBreakdown
                {
                    ItemTotal = new Money { Value = subtotal.ToString("F2") },
                    TaxTotal = new Money { Value = taxAmount.ToString("F2") }
                }
            }
        }
    }
};
```

### Option 2: Use Third-Party Service
Integrate with:
- TaxJar
- Avalara
- Vertex

## VAT Compliance (EU/UK)

### VAT Rates
- Standard: 15-27% (varies by country)
- Reduced: 5-13% (certain goods)
- Zero-rated: 0% (exports outside EU)

### VAT Invoice Requirements
Must include:
- Seller's VAT registration number
- Customer's VAT number (B2B)
- Breakdown of goods/services
- VAT amount per rate
- Total amount including VAT

### Reverse Charge (B2B)
For business customers in different EU countries:
- Don't charge VAT
- Customer self-assesses VAT in their country
- Validate customer VAT number via VIES

## Tax-Exempt Customers

### Handling Exemptions
1. Collect exemption certificate
2. Store in secure location
3. Validate periodically
4. Don't charge tax on future purchases

```csharp
if (customer.IsTaxExempt && customer.HasValidExemptionCertificate)
{
    paymentIntentOptions.AutomaticTax.Enabled = false;
}
```

## Resources
- **Stripe Tax Docs**: https://stripe.com/docs/tax
- **US Sales Tax**: https://www.sales-taxes.com
- **EU VAT**: https://ec.europa.eu/taxation_customs/vies/
- **UK VAT**: https://www.gov.uk/vat-registration
